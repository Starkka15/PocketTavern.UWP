using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Storage;
using PocketTavern.UWP.Data;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.ViewModels
{
    public class ExtensionsViewModel : ViewModelBase
    {
        private readonly JsExtensionStorage _jsStorage = new JsExtensionStorage();

        // ── Native extension toggles ──────────────────────────────────────────

        private bool _quickReplyEnabled;
        private bool _regexEnabled;
        private bool _tokenCounterEnabled;

        public bool QuickReplyEnabled
        {
            get => _quickReplyEnabled;
            set { Set(ref _quickReplyEnabled, value); App.Settings.SetExtQuickReplyEnabled(value); }
        }

        public bool RegexEnabled
        {
            get => _regexEnabled;
            set { Set(ref _regexEnabled, value); App.Settings.SetExtRegexEnabled(value); }
        }

        public bool TokenCounterEnabled
        {
            get => _tokenCounterEnabled;
            set { Set(ref _tokenCounterEnabled, value); App.Settings.SetExtTokenCounterEnabled(value); }
        }

        // ── JS extensions ─────────────────────────────────────────────────────

        private ObservableCollection<JsExtensionItem> _jsExtensions = new ObservableCollection<JsExtensionItem>();
        public ObservableCollection<JsExtensionItem> JsExtensions
        {
            get => _jsExtensions;
            set => Set(ref _jsExtensions, value);
        }

        private bool _isInstalling;
        public bool IsInstalling { get => _isInstalling; set => Set(ref _isInstalling, value); }

        private string _installError;
        public string InstallError { get => _installError; set => Set(ref _installError, value); }

        // ── JS extension settings ─────────────────────────────────────────────

        /// extensionId → (key → value)
        public Dictionary<string, Dictionary<string, object>> JsExtensionSettings { get; private set; }
            = new Dictionary<string, Dictionary<string, object>>();

        // ── Init ──────────────────────────────────────────────────────────────

        public void Load()
        {
            _quickReplyEnabled  = App.Settings.GetExtQuickReplyEnabled();
            _regexEnabled       = App.Settings.GetExtRegexEnabled();
            _tokenCounterEnabled = App.Settings.GetExtTokenCounterEnabled();
            OnPropertyChanged(nameof(QuickReplyEnabled));
            OnPropertyChanged(nameof(RegexEnabled));
            OnPropertyChanged(nameof(TokenCounterEnabled));

            RefreshJsExtensions();
        }

        // ── JS install ────────────────────────────────────────────────────────

        public async Task InstallFromUrlAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            IsInstalling = true;
            InstallError = null;
            try
            {
                await _jsStorage.InstallFromUrlAsync(url);
                RefreshJsExtensions();
            }
            catch (System.Exception ex)
            {
                InstallError = ex.Message ?? "Install failed";
            }
            finally { IsInstalling = false; }
        }

        public async Task InstallFromFileAsync(StorageFile file)
        {
            IsInstalling = true;
            InstallError = null;
            try
            {
                await _jsStorage.InstallFromFileAsync(file);
                RefreshJsExtensions();
            }
            catch (System.Exception ex)
            {
                InstallError = ex.Message ?? "Import failed";
            }
            finally { IsInstalling = false; }
        }

        public void Uninstall(string id)
        {
            _jsStorage.Uninstall(id);
            RefreshJsExtensions();
        }

        public void SetJsExtensionEnabled(string id, bool enabled)
        {
            _jsStorage.SetEnabled(id, enabled);
            RefreshJsExtensions();
        }

        public void UpdateJsSetting(string extensionId, string key, object value)
        {
            _jsStorage.UpdateExtensionSetting(extensionId, key, value);
            if (!JsExtensionSettings.TryGetValue(extensionId, out var extSettings))
                JsExtensionSettings[extensionId] = extSettings = new Dictionary<string, object>();
            extSettings[key] = value;
        }

        public void ClearInstallError() => InstallError = null;

        // ── Private ───────────────────────────────────────────────────────────

        private void RefreshJsExtensions()
        {
            var exts = _jsStorage.ListExtensions();
            JsExtensions.Clear();
            foreach (var e in exts) JsExtensions.Add(e);

            JsExtensionSettings = new Dictionary<string, Dictionary<string, object>>();
            foreach (var e in exts)
                JsExtensionSettings[e.Id] = _jsStorage.GetExtensionSettings(e.Id);
        }
    }
}
