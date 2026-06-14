using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PocketTavern.UWP.Data;

namespace PocketTavern.UWP.Views
{
    public sealed partial class ExtensionPanelPage : Page
    {
        private string _extensionId;
        private string _extensionDir;
        private bool _bundled;

        public ExtensionPanelPage() { this.InitializeComponent(); }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _extensionId = e.Parameter as string;
            if (string.IsNullOrEmpty(_extensionId))
            {
                ShowEmpty("No extension specified.");
                return;
            }

            var storage = new JsExtensionStorage();
            var ext = storage.ListExtensions().Find(x => x.Id == _extensionId);
            if (ext == null)
            {
                ShowEmpty($"Extension \"{_extensionId}\" not found.");
                return;
            }

            PageTitle.Text = ext.Name;
            _bundled = ext.Bundled;
            _extensionDir = _bundled
                ? Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "Extensions", _extensionId)
                : Path.Combine(ApplicationData.Current.LocalFolder.Path, "js_extensions", _extensionId);

            var htmlFile = Path.Combine(_extensionDir, "browser.html");
            if (!File.Exists(htmlFile))
                htmlFile = Path.Combine(_extensionDir, "panel.html");
            if (!File.Exists(htmlFile))
            {
                ShowEmpty($"Extension \"{ext.Name}\" does not provide a panel UI.");
                return;
            }

            LoadPanel(htmlFile);
        }

        private void ShowEmpty(string message)
        {
            EmptyState.Visibility = Visibility.Visible;
            PanelWebView.Visibility = Visibility.Collapsed;
            ReloadBtn.Visibility = Visibility.Collapsed;
            EmptySubtext.Text = message;
        }

        private async void LoadPanel(string htmlFile)
        {
            EmptyState.Visibility = Visibility.Collapsed;
            PanelWebView.Visibility = Visibility.Visible;
            ReloadBtn.Visibility = Visibility.Visible;

            Uri uri;
            if (_bundled)
            {
                var rel = "Assets/Extensions/" + _extensionId + "/" + Path.GetFileName(htmlFile);
                uri = new Uri("ms-appx-web:///" + rel.Replace('\\', '/'));
            }
            else
            {
                var rel = "js_extensions/" + _extensionId + "/" + Path.GetFileName(htmlFile);
                uri = new Uri("ms-appdata:///local/" + rel.Replace('\\', '/'));
            }

            PanelWebView.NavigationCompleted += OnPanelNavigationCompleted;
            PanelWebView.Navigate(uri);
        }

        private async void OnPanelNavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs e)
        {
            PanelWebView.NavigationCompleted -= OnPanelNavigationCompleted;
            if (!e.IsSuccess) return;

            try
            {
                await InjectBridgeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExtensionPanel] bridge inject failed: {ex.Message}");
            }
        }

        private async Task InjectBridgeAsync()
        {
            var shimPath = Path.Combine(
                Windows.ApplicationModel.Package.Current.InstalledLocation.Path,
                "Assets", "Extensions", "uwp_bridge_shim.js");
            if (!File.Exists(shimPath)) return;

            var shim = File.ReadAllText(shimPath);
            await PanelWebView.InvokeScriptAsync("eval", new[] { shim });

            var apiPath = Path.Combine(
                Windows.ApplicationModel.Package.Current.InstalledLocation.Path,
                "Assets", "Extensions", "pt_api.js");
            if (File.Exists(apiPath))
            {
                var api = File.ReadAllText(apiPath);
                await PanelWebView.InvokeScriptAsync("eval", new[] { api });
            }

            System.Diagnostics.Debug.WriteLine($"[ExtensionPanel] bridge injected for {_extensionId}");
        }

        private async void OnScriptNotify(object sender, NotifyEventArgs e)
        {
            try
            {
                var msg = JObject.Parse(e.Value);
                var m = msg.Value<string>("m");
                if (m == null) return;

                switch (m)
                {
                    case "log":
                        System.Diagnostics.Debug.WriteLine($"[ExtPanel:{_extensionId}] {msg.Value<string>("msg")}");
                        break;

                    case "sendMessage":
                    {
                        var text = msg.Value<string>("text");
                        if (!string.IsNullOrEmpty(text))
                            App.Extensions.FireMessageSendRequest(text);
                        break;
                    }

                    case "registerButtons":
                    {
                        var id = msg.Value<string>("id") ?? "";
                        var json = msg.Value<string>("json");
                        if (!string.IsNullOrEmpty(json))
                            App.Extensions.HandleRegisterButtons(id, json);
                        break;
                    }

                    case "clearButtons":
                        App.Extensions.HandleClearButtons(msg.Value<string>("id") ?? "");
                        break;

                    case "generateImage":
                        App.Extensions.FireImageGenerateRequest(
                            msg.Value<string>("prompt") ?? "",
                            msg.Value<string>("options") ?? "{}",
                            msg.Value<string>("cbId") ?? "");
                        break;

                    case "showEditDialog":
                        App.Extensions.FireEditDialogRequest(
                            msg.Value<string>("title") ?? "",
                            msg.Value<string>("fields") ?? "[]",
                            msg.Value<string>("cbId") ?? "");
                        break;

                    case "generateHidden":
                        App.Extensions.FireHiddenGenerateRequest(
                            msg.Value<string>("prompt") ?? "",
                            msg.Value<string>("cbId") ?? "");
                        break;

                    case "insertMessage":
                        App.Extensions.FireInsertMessageRequest(
                            msg.Value<string>("content") ?? "",
                            msg.Value<string>("options") ?? "{}");
                        break;

                    case "saveAllSettings":
                        break;

                    default:
                        System.Diagnostics.Debug.WriteLine($"[ExtPanel:{_extensionId}] unhandled notify: {m}");
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExtPanel] ScriptNotify error: {ex.Message}");
            }
        }

        private void OnBackClick(object sender, RoutedEventArgs e) => App.Navigation.GoBack();

        private async void OnReloadClick(object sender, RoutedEventArgs e)
        {
            var htmlFile = Path.Combine(_extensionDir, "browser.html");
            if (!File.Exists(htmlFile))
                htmlFile = Path.Combine(_extensionDir, "panel.html");
            if (File.Exists(htmlFile))
                LoadPanel(htmlFile);
        }
    }
}
