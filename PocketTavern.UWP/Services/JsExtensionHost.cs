using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PocketTavern.UWP.Data;

namespace PocketTavern.UWP.Services
{
    /// <summary>
    /// Manages the hidden WebView JS sandbox for extensions.
    /// JS→C# via ScriptNotify; C#→JS via InvokeScriptAsync("eval", [...]).
    /// </summary>
    public class JsExtensionHost
    {
        private WebView _webView;
        private readonly JsExtensionStorage _storage = new JsExtensionStorage();

        // ── Registered extension state ────────────────────────────────────────

        private readonly Dictionary<string, string> _promptInjections = new Dictionary<string, string>();
        // key = "{extId}:{injectionId}" → script text

        private readonly Dictionary<string, object[]> _promptInjectionMeta = new Dictionary<string, object[]>();
        // key = same → [pos, dep]

        private readonly Dictionary<string, List<object>> _buttonSets = new Dictionary<string, List<object>>();
        // key = extId → list of button objects

        private readonly Dictionary<string, List<object>> _headerButtonSets = new Dictionary<string, List<object>>();
        private readonly Dictionary<string, List<object>> _headerMenuSets   = new Dictionary<string, List<object>>();
        private readonly Dictionary<string, List<object>> _messageActionSets = new Dictionary<string, List<object>>();
        private readonly Dictionary<string, string> _outputFilters = new Dictionary<string, string>();
        // key = extId, value = regex pattern

        private readonly Dictionary<int, List<object>> _messageHeaders = new Dictionary<int, List<object>>();
        // idx → list of header objects

        private bool _loaded;

        // ── Events ────────────────────────────────────────────────────────────

        /// Fired when an extension calls PT.sendMessage(text).
        public event EventHandler<string> MessageSendRequested;

        /// Fired when an extension calls PT.showEditDialog(...)
        public event EventHandler<EditDialogRequest> EditDialogRequested;

        /// Fired when an extension calls PT.generateHidden(...)
        public event EventHandler<HiddenGenerateRequest> HiddenGenerateRequested;

        /// Fired when an extension calls PT.generateImage(...)
        public event EventHandler<ImageGenerateRequest> ImageGenerateRequested;

        // ── Init ──────────────────────────────────────────────────────────────

        public void Initialize(WebView webView)
        {
            _webView = webView;
            _webView.ScriptNotify += OnScriptNotify;
        }

        public async Task LoadAsync()
        {
            if (_webView == null) return;

            var html = BuildSandboxHtml();
            _webView.NavigateToString(html);

            // NavigateToString is not awaitable; we wait for NavigationCompleted
            await WaitForNavigationAsync();
            _loaded = true;
        }

        public async Task ReloadAsync()
        {
            _loaded = false;
            ClearAllState();
            await LoadAsync();
        }

        // ── C# → JS events ───────────────────────────────────────────────────

        public async Task DispatchEventAsync(string eventName, string dataJson = null)
        {
            if (!_loaded || _webView == null) return;
            var evtEscaped = EscapeJs(eventName);
            var dataArg = dataJson != null
                ? $"JSON.parse({JsonConvert.SerializeObject(dataJson)})"
                : "null";
            await EvalAsync($"window.__ptDispatchEvent('{evtEscaped}', {dataArg});");
        }

        public async Task UpdateContextAsync(string contextJson)
        {
            if (_webView == null) return;
            // contextJson is already valid JSON — assign directly
            await EvalAsync($"window.__ptContextJson = {contextJson ?? "{}"};");
        }

        public async Task PushMessageHeadersAsync(Dictionary<int, List<object>> headers)
        {
            if (_webView == null) return;
            var json = JsonConvert.SerializeObject(headers);
            await EvalAsync($"window.__ptMessageHeaders = {json};");
        }

        public async Task SetDisabledExtensionsAsync(IEnumerable<string> disabledIds)
        {
            if (_webView == null) return;
            var json = JsonConvert.SerializeObject(disabledIds.ToList());
            await EvalAsync($"window.__ptDisabledExtensions = {json};");
        }

        // ── Query state ───────────────────────────────────────────────────────

        /// Returns all active prompt injections as a list of {text, pos, dep} tuples.
        public IEnumerable<PromptInjection> GetPromptInjections()
        {
            foreach (var kv in _promptInjections)
            {
                var meta = _promptInjectionMeta.TryGetValue(kv.Key, out var m) ? m : new object[] { 0, 4 };
                yield return new PromptInjection
                {
                    Text     = kv.Value,
                    Position = meta[0] is long l ? (int)l : Convert.ToInt32(meta[0]),
                    Depth    = meta[1] is long l2 ? (int)l2 : Convert.ToInt32(meta[1])
                };
            }
        }

        /// Applies all registered output filters (regex replace with empty string) to AI text.
        public string ApplyOutputFilters(string text)
        {
            foreach (var kv in _outputFilters)
            {
                try { text = Regex.Replace(text, kv.Value, ""); }
                catch { /* bad regex — skip */ }
            }
            return text;
        }

        public IReadOnlyDictionary<string, List<object>> GetButtonSets() => _buttonSets;
        public IReadOnlyDictionary<int, List<object>> GetMessageHeaders() => _messageHeaders;

        // ── Callback completions (C# → JS resolve) ───────────────────────────

        public async Task ResolveEditDialogAsync(string cbId, string resultJson)
        {
            if (_webView == null) return;
            var escaped = (resultJson ?? "null").Replace("\\", "\\\\").Replace("'", "\\'");
            await EvalAsync($"window.__ptEditDialogResult && window.__ptEditDialogResult('{cbId}', '{escaped}');");
        }

        public async Task ResolveHiddenGenerateAsync(string cbId, string resultText)
        {
            if (_webView == null) return;
            var escaped = (resultText ?? "").Replace("\\", "\\\\").Replace("'", "\\'");
            await EvalAsync($"window.__ptHiddenGenerateResult && window.__ptHiddenGenerateResult('{cbId}', '{escaped}');");
        }

        public async Task ResolveImageGenerateAsync(string cbId, string resultJson)
        {
            if (_webView == null) return;
            var escaped = (resultJson ?? "null").Replace("\\", "\\\\").Replace("'", "\\'");
            await EvalAsync($"window.__ptImageGenerateResult && window.__ptImageGenerateResult('{cbId}', '{escaped}');");
        }

        // ── ScriptNotify handler ──────────────────────────────────────────────

        private async void OnScriptNotify(object sender, NotifyEventArgs e)
        {
            JObject msg;
            try { msg = JObject.Parse(e.Value); }
            catch { return; }

            var m = msg.Value<string>("m");
            switch (m)
            {
                case "setPromptInjection":
                {
                    var id    = msg.Value<string>("id")    ?? "";
                    var extId = msg.Value<string>("extId") ?? "";
                    var key   = $"{extId}:{id}";
                    var text = msg.Value<string>("text");
                    if (string.IsNullOrEmpty(text))
                    {
                        _promptInjections.Remove(key);
                        _promptInjectionMeta.Remove(key);
                    }
                    else
                    {
                        _promptInjections[key] = text;
                        _promptInjectionMeta[key] = new object[]
                        {
                            msg["pos"] ?? (object)0,
                            msg["dep"] ?? (object)4
                        };
                    }
                    break;
                }

                case "registerButtons":
                {
                    var id   = msg.Value<string>("id") ?? "";
                    var json = msg.Value<string>("json");
                    try
                    {
                        var buttons = JsonConvert.DeserializeObject<List<object>>(json ?? "[]");
                        _buttonSets[id] = buttons ?? new List<object>();
                    }
                    catch { }
                    break;
                }

                case "clearButtons":
                    _buttonSets.Remove(msg.Value<string>("id") ?? "");
                    break;

                case "setMessageHeader":
                {
                    var idx = msg.Value<int>("idx");
                    if (!_messageHeaders.ContainsKey(idx)) _messageHeaders[idx] = new List<object>();
                    var extId = msg.Value<string>("extId") ?? "";
                    // Replace or add header for this extId
                    _messageHeaders[idx].RemoveAll(h => (h as JObject)?.Value<string>("extId") == extId);
                    _messageHeaders[idx].Add(new { text = msg.Value<string>("text"), extId = extId, collapsible = msg.Value<string>("collapsible") });
                    break;
                }

                case "clearMessageHeader":
                {
                    var idx = msg.Value<int>("idx");
                    _messageHeaders.Remove(idx);
                    break;
                }

                case "clearAllHeaders":
                    _messageHeaders.Clear();
                    break;

                case "registerHeaderButtons":
                {
                    var id = msg.Value<string>("id") ?? "";
                    try { _headerButtonSets[id] = JsonConvert.DeserializeObject<List<object>>(msg.Value<string>("json") ?? "[]") ?? new List<object>(); }
                    catch { }
                    break;
                }

                case "clearHeaderButtons":
                    _headerButtonSets.Remove(msg.Value<string>("id") ?? "");
                    break;

                case "registerHeaderMenu":
                {
                    var id = msg.Value<string>("id") ?? "";
                    try { _headerMenuSets[id] = JsonConvert.DeserializeObject<List<object>>(msg.Value<string>("json") ?? "[]") ?? new List<object>(); }
                    catch { }
                    break;
                }

                case "clearHeaderMenu":
                    _headerMenuSets.Remove(msg.Value<string>("id") ?? "");
                    break;

                case "registerMessageActions":
                {
                    var id = msg.Value<string>("id") ?? "";
                    try { _messageActionSets[id] = JsonConvert.DeserializeObject<List<object>>(msg.Value<string>("json") ?? "[]") ?? new List<object>(); }
                    catch { }
                    break;
                }

                case "clearMessageActions":
                    _messageActionSets.Remove(msg.Value<string>("id") ?? "");
                    break;

                case "registerOutputFilter":
                    _outputFilters[msg.Value<string>("id") ?? ""] = msg.Value<string>("pattern") ?? "";
                    break;

                case "clearOutputFilter":
                    _outputFilters.Remove(msg.Value<string>("id") ?? "");
                    break;

                case "sendMessage":
                    MessageSendRequested?.Invoke(this, msg.Value<string>("text") ?? "");
                    break;

                case "saveAllSettings":
                    // Extensions call this to persist their own settings blob
                    // We store it per-extension via App.Settings raw key
                    break;

                case "showEditDialog":
                    EditDialogRequested?.Invoke(this, new EditDialogRequest
                    {
                        Title      = msg.Value<string>("title") ?? "",
                        FieldsJson = msg.Value<string>("fields") ?? "[]",
                        CbId       = msg.Value<string>("cbId") ?? ""
                    });
                    break;

                case "generateHidden":
                    HiddenGenerateRequested?.Invoke(this, new HiddenGenerateRequest
                    {
                        Prompt = msg.Value<string>("prompt") ?? "",
                        CbId   = msg.Value<string>("cbId") ?? ""
                    });
                    break;

                case "generateImage":
                    ImageGenerateRequested?.Invoke(this, new ImageGenerateRequest
                    {
                        Prompt      = msg.Value<string>("prompt") ?? "",
                        OptionsJson = msg.Value<string>("options") ?? "{}",
                        CbId        = msg.Value<string>("cbId") ?? ""
                    });
                    break;

                case "log":
                    System.Diagnostics.Debug.WriteLine($"[JsExt] {msg.Value<string>("msg")}");
                    break;

                case "insertMessage":
                    // Raise as a send-message event (simplified: treat as user message insertion)
                    MessageSendRequested?.Invoke(this, msg.Value<string>("content") ?? "");
                    break;
            }
        }

        // ── Build sandbox HTML ────────────────────────────────────────────────

        private string BuildSandboxHtml()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'></head><body><script>");

            // 1. UWP bridge shim
            sb.AppendLine(ReadAsset("uwp_bridge_shim.js"));

            // 2. pt_api.js
            sb.AppendLine(_storage.GetPtApiScript());

            // 3. Each enabled extension (wrapped with extId context + error guard)
            var extensions = _storage.ListExtensions().Where(e => e.Enabled).ToList();
            foreach (var ext in extensions)
            {
                var script = _storage.GetExtensionScript(ext);
                if (string.IsNullOrWhiteSpace(script)) continue;

                sb.AppendLine($"(function() {{");
                sb.AppendLine($"  window.__ptCurrentExtId = '{EscapeJs(ext.Id)}';");
                sb.AppendLine($"  try {{");
                sb.AppendLine(script);
                sb.AppendLine($"  }} catch(e) {{");
                sb.AppendLine($"    if (window.PtBridge) PtBridge.log('[{EscapeJs(ext.Id)}] load error: ' + e.message);");
                sb.AppendLine($"  }}");
                sb.AppendLine($"  window.__ptCurrentExtId = null;");
                sb.AppendLine($"}})();");
            }

            sb.AppendLine("</script></body></html>");
            return sb.ToString();
        }

        private static string ReadAsset(string filename)
        {
            var path = System.IO.Path.Combine(
                Windows.ApplicationModel.Package.Current.InstalledLocation.Path,
                "Assets", "Extensions", filename);
            return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : $"/* missing: {filename} */";
        }

        private static string EscapeJs(string s)
            => s?.Replace("\\", "\\\\").Replace("'", "\\'") ?? "";

        // ── Navigation wait ───────────────────────────────────────────────────

        private Task WaitForNavigationAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            WebViewNavigationCompletedEventHandler handler = null;
            handler = (s, e) =>
            {
                _webView.NavigationCompleted -= handler;
                tcs.TrySetResult(true);
            };
            _webView.NavigationCompleted += handler;
            return tcs.Task;
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private async Task EvalAsync(string script)
        {
            try
            {
                await _webView.InvokeScriptAsync("eval", new[] { script });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[JsExtensionHost] eval error: {ex.Message}");
            }
        }

        private void ClearAllState()
        {
            _promptInjections.Clear();
            _promptInjectionMeta.Clear();
            _buttonSets.Clear();
            _headerButtonSets.Clear();
            _headerMenuSets.Clear();
            _messageActionSets.Clear();
            _outputFilters.Clear();
            _messageHeaders.Clear();
        }
    }

    // ── Supporting types ──────────────────────────────────────────────────────

    public class PromptInjection
    {
        public string Text     { get; set; }
        public int    Position { get; set; }  // 0=before, 1=after, etc. — mirrors SillyTavern
        public int    Depth    { get; set; }
    }

    public class EditDialogRequest
    {
        public string Title      { get; set; }
        public string FieldsJson { get; set; }
        public string CbId       { get; set; }
    }

    public class HiddenGenerateRequest
    {
        public string Prompt { get; set; }
        public string CbId   { get; set; }
    }

    public class ImageGenerateRequest
    {
        public string Prompt      { get; set; }
        public string OptionsJson { get; set; }
        public string CbId        { get; set; }
    }
}
