using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.Data
{
    /// <summary>
    /// Manages JavaScript extension lifecycle: bundled assets, user-installed extensions,
    /// enable/disable state, per-extension settings, and install/uninstall.
    /// Mirrors Android's JsExtensionStorage.
    /// </summary>
    public class JsExtensionStorage
    {
        private readonly string _jsExtDir;
        private readonly string _settingsFile;
        private readonly string _enabledFile;
        private readonly HttpClient _http = new HttpClient();

        public JsExtensionStorage()
        {
            var local = ApplicationData.Current.LocalFolder.Path;
            _jsExtDir = Path.Combine(local, "js_extensions");
            Directory.CreateDirectory(_jsExtDir);
            _settingsFile = Path.Combine(_jsExtDir, "_settings.json");
            _enabledFile  = Path.Combine(_jsExtDir, "_enabled.json");
        }

        // ── Listing ───────────────────────────────────────────────────────────

        public List<JsExtensionItem> ListExtensions()
        {
            var enabledMap = LoadEnabledMap();
            var result = new List<JsExtensionItem>();
            var seenIds = new HashSet<string>();

            // Bundled extensions from Assets/Extensions/
            var assetExtDir = Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path,
                                           "Assets", "Extensions");
            if (Directory.Exists(assetExtDir))
            {
                foreach (var dir in Directory.GetDirectories(assetExtDir))
                {
                    var id = Path.GetFileName(dir);
                    if (id == null) continue;
                    var indexJs = Path.Combine(dir, "index.js");
                    if (!File.Exists(indexJs)) continue;
                    var manifest = LoadManifest(dir);
                    seenIds.Add(id);
                    result.Add(new JsExtensionItem
                    {
                        Id          = id,
                        Name        = manifest.Value<string>("name") ?? id,
                        Version     = manifest.Value<string>("version") ?? "1.0.0",
                        Description = manifest.Value<string>("description") ?? "",
                        Author      = manifest.Value<string>("author") ?? "",
                        SourceUrl   = "",
                        Enabled     = enabledMap.TryGetValue(id, out var en) ? en : true,
                        Bundled     = true
                    });
                }
            }

            // User-installed extensions from LocalFolder
            foreach (var dir in Directory.GetDirectories(_jsExtDir))
            {
                var id = Path.GetFileName(dir);
                if (id == null || id.StartsWith("_") || seenIds.Contains(id)) continue;
                var indexJs = Path.Combine(dir, "index.js");
                if (!File.Exists(indexJs)) continue;
                var manifest = LoadManifest(dir);
                result.Add(new JsExtensionItem
                {
                    Id          = id,
                    Name        = manifest.Value<string>("name") ?? id,
                    Version     = manifest.Value<string>("version") ?? "1.0.0",
                    Description = manifest.Value<string>("description") ?? "",
                    Author      = manifest.Value<string>("author") ?? "",
                    SourceUrl   = manifest.Value<string>("sourceUrl") ?? "",
                    Enabled     = enabledMap.TryGetValue(id, out var en) ? en : true,
                    Bundled     = false
                });
            }

            result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        // ── Install from URL ──────────────────────────────────────────────────

        public async Task<JsExtensionItem> InstallFromUrlAsync(string url)
        {
            var scriptUrl = url.TrimEnd('/').EndsWith(".js", StringComparison.OrdinalIgnoreCase)
                ? url.Trim()
                : url.TrimEnd('/') + "/index.js";
            var baseUrl = scriptUrl.EndsWith("/index.js")
                ? scriptUrl.Substring(0, scriptUrl.Length - "/index.js".Length)
                : scriptUrl.Substring(0, scriptUrl.LastIndexOf('/'));

            // Try manifest.json
            JObject manifest = null;
            try
            {
                var manifestText = await _http.GetStringAsync(baseUrl + "/manifest.json");
                manifest = JObject.Parse(manifestText);
            }
            catch { /* optional */ }

            var id = (manifest?.Value<string>("id")
                ?? baseUrl.Split('/').Last()
                          .ToLowerInvariant()
                          .Replace(' ', '_'))
                .If(s => !string.IsNullOrWhiteSpace(s), s => s, _ => $"ext_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");

            var scriptText = await _http.GetStringAsync(scriptUrl);
            var extDir = Directory.CreateDirectory(Path.Combine(_jsExtDir, id)).FullName;

            File.WriteAllText(Path.Combine(extDir, "index.js"), scriptText);
            var name = manifest?.Value<string>("display_name") ?? manifest?.Value<string>("name") ?? id;
            WriteManifest(extDir, id, name, manifest, baseUrl);

            return new JsExtensionItem
            {
                Id = id, Name = name,
                Version     = manifest?.Value<string>("version") ?? "1.0.0",
                Description = manifest?.Value<string>("description") ?? "",
                Author      = manifest?.Value<string>("author") ?? "",
                SourceUrl   = baseUrl,
                Enabled     = true,
                Bundled     = false
            };
        }

        // ── Install from file (.js or .zip) ───────────────────────────────────

        public async Task<JsExtensionItem> InstallFromFileAsync(Windows.Storage.StorageFile file)
        {
            var name = file.Name;
            if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return await InstallFromZipAsync(file);
            return await InstallFromJsFileAsync(file);
        }

        private async Task<JsExtensionItem> InstallFromJsFileAsync(Windows.Storage.StorageFile file)
        {
            var scriptText = await FileIO.ReadTextAsync(file);
            var baseName = Path.GetFileNameWithoutExtension(file.Name)
                .ToLowerInvariant().Replace(' ', '_').If(
                    s => !string.IsNullOrWhiteSpace(s), s => s, _ => $"ext_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");

            var extDir = Directory.CreateDirectory(Path.Combine(_jsExtDir, baseName)).FullName;
            File.WriteAllText(Path.Combine(extDir, "index.js"), scriptText);
            WriteManifest(extDir, baseName, baseName, null, "");

            return new JsExtensionItem { Id = baseName, Name = baseName, Version = "1.0.0",
                Description = "Imported from file", Enabled = true };
        }

        private async Task<JsExtensionItem> InstallFromZipAsync(Windows.Storage.StorageFile file)
        {
            var entries = new Dictionary<string, byte[]>();
            using (var stream = await file.OpenStreamForReadAsync())
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (var entry in zip.Entries)
                {
                    if (entry.Length == 0) continue;
                    using (var ms = new MemoryStream())
                    using (var es = entry.Open())
                    {
                        await es.CopyToAsync(ms);
                        entries[entry.FullName.Replace('\\', '/')] = ms.ToArray();
                    }
                }
            }

            // Find index.js — root or inside single top-level folder
            var indexPath = entries.Keys.FirstOrDefault(k => k == "index.js")
                         ?? entries.Keys.FirstOrDefault(k => k.EndsWith("/index.js") && k.Count(c => c == '/') == 1);
            if (indexPath == null) throw new InvalidOperationException("Zip must contain index.js");

            var prefix = indexPath == "index.js" ? "" : indexPath.Substring(0, indexPath.Length - "index.js".Length);
            var manifestPath = prefix + "manifest.json";
            JObject manifest = null;
            if (entries.TryGetValue(manifestPath, out var manifestBytes))
            {
                try { manifest = JObject.Parse(System.Text.Encoding.UTF8.GetString(manifestBytes)); }
                catch { /* ignore */ }
            }

            var id = (manifest?.Value<string>("id")
                ?? prefix.TrimEnd('/').Split('/').Last().ToLowerInvariant().Replace(' ', '_'))
                .If(s => !string.IsNullOrWhiteSpace(s), s => s, _ => $"ext_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");

            var extDir = Directory.CreateDirectory(Path.Combine(_jsExtDir, id)).FullName;
            foreach (var kv in entries)
            {
                if (!kv.Key.StartsWith(prefix)) continue;
                var relPath = kv.Key.Substring(prefix.Length).TrimStart('/');
                if (string.IsNullOrEmpty(relPath)) continue;
                var outPath = Path.Combine(extDir, relPath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                File.WriteAllBytes(outPath, kv.Value);
            }

            var name = manifest?.Value<string>("display_name") ?? manifest?.Value<string>("name") ?? id;
            WriteManifest(extDir, id, name, manifest, "");

            return new JsExtensionItem
            {
                Id = id, Name = name,
                Version     = manifest?.Value<string>("version") ?? "1.0.0",
                Description = manifest?.Value<string>("description") ?? "",
                Author      = manifest?.Value<string>("author") ?? "",
                Enabled     = true
            };
        }

        // ── Uninstall / Enable ────────────────────────────────────────────────

        public void Uninstall(string id)
        {
            var dir = Path.Combine(_jsExtDir, id);
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            var map = LoadEnabledMap();
            map.Remove(id);
            SaveEnabledMap(map);
        }

        public void SetEnabled(string id, bool enabled)
        {
            var map = LoadEnabledMap();
            map[id] = enabled;
            SaveEnabledMap(map);
        }

        // ── Settings ──────────────────────────────────────────────────────────

        public Dictionary<string, object> GetExtensionSettings(string extensionId)
        {
            var all = LoadAllSettings();
            if (!all.TryGetValue(extensionId, out var extSettings)) return new Dictionary<string, object>();
            return extSettings;
        }

        public void UpdateExtensionSetting(string extensionId, string key, object value)
        {
            var all = LoadAllSettings();
            if (!all.TryGetValue(extensionId, out var extSettings))
                extSettings = new Dictionary<string, object>();
            extSettings[key] = value;
            all[extensionId] = extSettings;
            SaveAllSettings(all);
        }

        // ── Script access (for JS sandbox) ───────────────────────────────────

        public string GetPtApiScript()
        {
            var path = Path.Combine(
                Windows.ApplicationModel.Package.Current.InstalledLocation.Path,
                "Assets", "Extensions", "pt_api.js");
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }

        public string GetExtensionScript(JsExtensionItem ext)
        {
            if (ext.Bundled)
            {
                var path = Path.Combine(
                    Windows.ApplicationModel.Package.Current.InstalledLocation.Path,
                    "Assets", "Extensions", ext.Id, "index.js");
                return File.Exists(path) ? File.ReadAllText(path) : "";
            }
            var userPath = Path.Combine(_jsExtDir, ext.Id, "index.js");
            return File.Exists(userPath) ? File.ReadAllText(userPath) : "";
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private Dictionary<string, bool> LoadEnabledMap()
        {
            if (!File.Exists(_enabledFile)) return new Dictionary<string, bool>();
            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, bool>>(File.ReadAllText(_enabledFile))
                       ?? new Dictionary<string, bool>();
            }
            catch { return new Dictionary<string, bool>(); }
        }

        private void SaveEnabledMap(Dictionary<string, bool> map)
            => File.WriteAllText(_enabledFile, JsonConvert.SerializeObject(map));

        private Dictionary<string, Dictionary<string, object>> LoadAllSettings()
        {
            if (!File.Exists(_settingsFile)) return new Dictionary<string, Dictionary<string, object>>();
            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(
                           File.ReadAllText(_settingsFile))
                       ?? new Dictionary<string, Dictionary<string, object>>();
            }
            catch { return new Dictionary<string, Dictionary<string, object>>(); }
        }

        private void SaveAllSettings(Dictionary<string, Dictionary<string, object>> all)
            => File.WriteAllText(_settingsFile, JsonConvert.SerializeObject(all));

        private JObject LoadManifest(string dir)
        {
            var f = Path.Combine(dir, "manifest.json");
            if (!File.Exists(f)) return new JObject();
            try { return JObject.Parse(File.ReadAllText(f)); }
            catch { return new JObject(); }
        }

        private void WriteManifest(string extDir, string id, string name, JObject source, string sourceUrl)
        {
            var obj = new JObject
            {
                ["id"]          = id,
                ["name"]        = name,
                ["version"]     = source?.Value<string>("version") ?? "1.0.0",
                ["description"] = source?.Value<string>("description") ?? "",
                ["author"]      = source?.Value<string>("author") ?? "",
                ["sourceUrl"]   = sourceUrl
            };
            File.WriteAllText(Path.Combine(extDir, "manifest.json"), obj.ToString());
        }
    }

    // Small helper — equivalent of Kotlin's takeIf/let
    internal static class StringExtensions
    {
        public static TResult If<T, TResult>(this T value, Func<T, bool> condition,
            Func<T, TResult> thenFn, Func<T, TResult> elseFn)
            => condition(value) ? thenFn(value) : elseFn(value);
    }
}
