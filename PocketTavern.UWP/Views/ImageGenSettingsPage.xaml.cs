using System;
using System.Collections.Generic;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using PocketTavern.UWP.Controls;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class ImageGenSettingsPage : Page
    {
        private readonly ImageGenSettingsViewModel _vm = new ImageGenSettingsViewModel();
        private bool _suppress;

        // Dynamic controls that need to be shown/hidden based on backend
        private ComboBox _backendCombo;
        private SpacedPanel _urlSection;
        private TextBox _sdUrlBox;
        private TextBox _comfyUrlBox;
        private SpacedPanel _apiKeySection;
        private PasswordBox _apiKeyBox;
        private SpacedPanel _modelSection;
        private ComboBox _dalleModelCombo;
        private ComboBox _pollinationsModelCombo;
        private TextBox _hfModelBox;
        private SpacedPanel _testSection;
        private TextBlock _testResultLabel;
        private SpacedPanel _samplerSection;
        private ComboBox _samplerCombo;
        private SpacedPanel _sdModelSection;
        private ComboBox _sdModelCombo;
        private SpacedPanel _stepsSection;
        private Slider _stepsSlider;
        private TextBlock _stepsLabel;
        private SpacedPanel _cfgSection;
        private Slider _cfgSlider;
        private TextBlock _cfgLabel;
        private SpacedPanel _seedSection;
        private TextBox _seedBox;
        private SpacedPanel _clipSkipSection;
        private Slider _clipSkipSlider;
        private TextBlock _clipSkipLabel;
        private StackPanel _negativePromptSection;
        private TextBox _negativePromptBox;
        private TextBlock _resolutionLabel;

        // Backend string values matching the combo index
        private static readonly string[] BackendKeys = { "SD_WEBUI", "COMFYUI", "DALLE", "STABILITY", "POLLINATIONS", "HUGGINGFACE" };
        private static readonly string[] BackendDisplays = { "SD WebUI / Forge", "ComfyUI", "DALL-E (OpenAI)", "Stability AI", "Pollinations", "HuggingFace" };

        private ToggleSwitch _enabledToggle;
        private StackPanel _settingsBody;

        public ImageGenSettingsPage() { this.InitializeComponent(); }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _vm.Load();
            BuildContent();
        }

        private void OnBackClick(object sender, RoutedEventArgs e) => App.Navigation.GoBack();

        // ── Content builder ───────────────────────────────────────────────────

        private void BuildContent()
        {
            ContentPanel.Children.Clear();

            var accent    = (SolidColorBrush)Application.Current.Resources["AccentPrimaryBrush"];
            var textPri   = (SolidColorBrush)Application.Current.Resources["TextPrimaryBrush"];
            var textSec   = (SolidColorBrush)Application.Current.Resources["TextSecondaryBrush"];
            var cardBg    = (SolidColorBrush)Application.Current.Resources["BackgroundCardBrush"];
            var surfaceBg = (SolidColorBrush)Application.Current.Resources["BackgroundSurfaceBrush"];

            var cfg = _vm.Config;

            // ── ENABLED TOGGLE ────────────────────────────────────────────────
            var enabledCard = new Border
            {
                Margin = new Thickness(12, 4, 12, 8),
                Padding = new Thickness(16, 12, 16, 12),
                CornerRadius = new CornerRadius(10),
                Background = cardBg
            };
            var enabledRow = new Grid();
            enabledRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new Windows.UI.Xaml.GridLength(1, Windows.UI.Xaml.GridUnitType.Star) });
            enabledRow.ColumnDefinitions.Add(new ColumnDefinition { Width = Windows.UI.Xaml.GridLength.Auto });
            var enabledInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            enabledInfo.Children.Add(new TextBlock { Text = "Enable Image Generation", FontSize = 15, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = textPri });
            enabledInfo.Children.Add(new TextBlock { Text = "Generate images from chat messages", FontSize = 12, Foreground = textSec, Margin = new Thickness(0, 2, 0, 0) });
            enabledRow.Children.Add(enabledInfo);
            _enabledToggle = new ToggleSwitch { IsOn = cfg.Enabled, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, -8, 0) };
            _enabledToggle.Toggled += (s, ev) =>
            {
                cfg.Enabled = _enabledToggle.IsOn;
                _vm.Save();
                if (_settingsBody != null)
                    _settingsBody.Visibility = _enabledToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
            };
            Grid.SetColumn(_enabledToggle, 1);
            enabledRow.Children.Add(_enabledToggle);
            enabledCard.Child = enabledRow;
            ContentPanel.Children.Add(enabledCard);

            // All settings below are hidden when disabled
            _settingsBody = new StackPanel();
            _settingsBody.Visibility = cfg.Enabled ? Visibility.Visible : Visibility.Collapsed;
            ContentPanel.Children.Add(_settingsBody);

            // ── BACKEND ──────────────────────────────────────────────────────
            _settingsBody.Children.Add(MakeSectionLabel("BACKEND", accent));

            var backendCard = MakeCard(cardBg);
            var backendStack = (SpacedPanel)backendCard.Child;

            _backendCombo = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 0)
            };
            foreach (var d in BackendDisplays)
                _backendCombo.Items.Add(d);

            int backendIdx = Array.IndexOf(BackendKeys, cfg.ActiveBackend);
            _backendCombo.SelectedIndex = backendIdx >= 0 ? backendIdx : 0;
            _backendCombo.SelectionChanged += OnBackendChanged;
            backendStack.Children.Add(_backendCombo);
            _settingsBody.Children.Add(backendCard);

            // ── CONNECTION ────────────────────────────────────────────────────
            _settingsBody.Children.Add(MakeSectionLabel("CONNECTION", accent));

            // URL section (SD_WEBUI / COMFYUI)
            _urlSection = new SpacedPanel { Spacing = 8 };
            var urlCard = MakeCardWith(_urlSection, cardBg);

            _sdUrlBox = MakeTextBox("SD WebUI URL", "http://192.168.1.100:7860", cfg.SdWebuiUrl, textPri);
            _sdUrlBox.LostFocus += (s, ev) => { cfg.SdWebuiUrl = _sdUrlBox.Text.Trim(); _vm.Save(); };
            _urlSection.Children.Add(MakeLabeledControl("SD WebUI URL", _sdUrlBox, textSec));

            _comfyUrlBox = MakeTextBox("ComfyUI URL", "http://192.168.1.100:8188", cfg.ComfyuiUrl, textPri);
            _comfyUrlBox.LostFocus += (s, ev) => { cfg.ComfyuiUrl = _comfyUrlBox.Text.Trim(); _vm.Save(); };
            _urlSection.Children.Add(MakeLabeledControl("ComfyUI URL", _comfyUrlBox, textSec));

            _testSection = new SpacedPanel { Spacing = 6, Margin = new Thickness(0, 4, 0, 0) };
            var testBtn = new Button
            {
                Content = "Test Connection",
                HorizontalAlignment = HorizontalAlignment.Left
            };
            testBtn.Click += async (s, ev) =>
            {
                await _vm.TestConnectionAsync();
                _testResultLabel.Text = _vm.TestResult ?? "";
            };
            _testResultLabel = new TextBlock
            {
                FontSize = 12,
                Foreground = textSec,
                TextWrapping = TextWrapping.Wrap
            };
            _testSection.Children.Add(testBtn);
            _testSection.Children.Add(_testResultLabel);
            _urlSection.Children.Add(_testSection);

            _settingsBody.Children.Add(urlCard);

            // API Key section (DALLE / STABILITY / HUGGINGFACE / POLLINATIONS)
            _apiKeySection = new SpacedPanel { Spacing = 8 };
            var apiKeyCard = MakeCardWith(_apiKeySection, cardBg);

            _apiKeyBox = new PasswordBox
            {
                PlaceholderText = "Enter API Key",
                Margin = new Thickness(0, 4, 0, 0)
            };
            _apiKeyBox.LostFocus += OnApiKeyLostFocus;
            _apiKeySection.Children.Add(MakeLabeledControl("API Key", _apiKeyBox, textSec));

            _settingsBody.Children.Add(apiKeyCard);

            // Model section
            _modelSection = new SpacedPanel { Spacing = 8 };
            var modelCard = MakeCardWith(_modelSection, cardBg);

            _dalleModelCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            foreach (var m in new[] { "dall-e-3", "dall-e-2" })
                _dalleModelCombo.Items.Add(m);
            _dalleModelCombo.SelectedItem = cfg.DalleModel ?? "dall-e-3";
            _dalleModelCombo.SelectionChanged += (s, ev) =>
            {
                if (!_suppress && _dalleModelCombo.SelectedItem is string m) { cfg.DalleModel = m; _vm.Save(); }
            };
            _modelSection.Children.Add(MakeLabeledControl("Model", _dalleModelCombo, textSec));

            _pollinationsModelCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            foreach (var m in new[] { "flux", "flux-realism", "flux-anime", "flux-3d", "flux-cablyai", "turbo" })
                _pollinationsModelCombo.Items.Add(m);
            _pollinationsModelCombo.SelectedItem = cfg.PollinationsModel ?? "flux";
            _pollinationsModelCombo.SelectionChanged += (s, ev) =>
            {
                if (!_suppress && _pollinationsModelCombo.SelectedItem is string m) { cfg.PollinationsModel = m; _vm.Save(); }
            };
            _modelSection.Children.Add(MakeLabeledControl("Model", _pollinationsModelCombo, textSec));

            _hfModelBox = MakeTextBox("Model", "stabilityai/stable-diffusion-xl-base-1.0", cfg.HuggingfaceModel, textPri);
            _hfModelBox.LostFocus += (s, ev) => { cfg.HuggingfaceModel = _hfModelBox.Text.Trim(); _vm.Save(); };
            _modelSection.Children.Add(MakeLabeledControl("Model", _hfModelBox, textSec));

            _settingsBody.Children.Add(modelCard);

            // ── PARAMETERS ────────────────────────────────────────────────────
            _settingsBody.Children.Add(MakeSectionLabel("PARAMETERS", accent));
            var paramCard = MakeCard(cardBg);
            var paramStack = (SpacedPanel)paramCard.Child;

            // SD WebUI Model
            _sdModelSection = new SpacedPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };
            var sdModelHeader = new Grid();
            sdModelHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new Windows.UI.Xaml.GridLength(1, Windows.UI.Xaml.GridUnitType.Star) });
            sdModelHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = Windows.UI.Xaml.GridLength.Auto });
            sdModelHeader.Children.Add(new TextBlock { Text = "Model", FontSize = 12, Foreground = textSec, VerticalAlignment = VerticalAlignment.Center });
            var fetchModelsBtn = new Button { Content = "Fetch", Padding = new Thickness(10, 4, 10, 4), FontSize = 12 };
            Grid.SetColumn(fetchModelsBtn, 1);
            sdModelHeader.Children.Add(fetchModelsBtn);
            _sdModelSection.Children.Add(sdModelHeader);
            _sdModelCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            if (!string.IsNullOrEmpty(cfg.SdModel)) _sdModelCombo.Items.Add(cfg.SdModel);
            if (_sdModelCombo.Items.Count > 0) _sdModelCombo.SelectedIndex = 0;
            _sdModelCombo.SelectionChanged += (s, ev) =>
            {
                if (!_suppress && _sdModelCombo.SelectedItem is string m) { cfg.SdModel = m; _vm.Save(); }
            };
            fetchModelsBtn.Click += async (s, ev) =>
            {
                fetchModelsBtn.IsEnabled = false;
                var models = await _vm.FetchModelsAsync();
                _suppress = true;
                _sdModelCombo.Items.Clear();
                foreach (var m in models) _sdModelCombo.Items.Add(m);
                var idx = models.IndexOf(cfg.SdModel ?? "");
                _sdModelCombo.SelectedIndex = idx >= 0 ? idx : (models.Count > 0 ? 0 : -1);
                if (_sdModelCombo.SelectedItem is string sel) cfg.SdModel = sel;
                _suppress = false;
                _vm.Save();
                fetchModelsBtn.IsEnabled = true;
            };
            _sdModelSection.Children.Add(_sdModelCombo);
            paramStack.Children.Add(_sdModelSection);

            // Sampler
            _samplerSection = new SpacedPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };
            var samplerHeader = new Grid();
            samplerHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new Windows.UI.Xaml.GridLength(1, Windows.UI.Xaml.GridUnitType.Star) });
            samplerHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = Windows.UI.Xaml.GridLength.Auto });
            samplerHeader.Children.Add(new TextBlock { Text = "Sampler", FontSize = 12, Foreground = textSec, VerticalAlignment = VerticalAlignment.Center });
            var fetchSamplersBtn = new Button { Content = "Fetch", Padding = new Thickness(10, 4, 10, 4), FontSize = 12 };
            Grid.SetColumn(fetchSamplersBtn, 1);
            samplerHeader.Children.Add(fetchSamplersBtn);
            _samplerSection.Children.Add(samplerHeader);
            _samplerCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            var defaultSamplers = new[] { "Euler", "Euler a", "DPM++ 2M", "DPM++ 2M Karras", "DPM++ SDE", "DDIM", "LMS", "Heun" };
            foreach (var s in defaultSamplers) _samplerCombo.Items.Add(s);
            var samplerIdx = System.Array.IndexOf(defaultSamplers, cfg.Sampler ?? "");
            if (samplerIdx < 0) { _samplerCombo.Items.Add(cfg.Sampler ?? "Euler"); samplerIdx = _samplerCombo.Items.Count - 1; }
            _samplerCombo.SelectedIndex = samplerIdx >= 0 ? samplerIdx : 0;
            _samplerCombo.SelectionChanged += (s, ev) =>
            {
                if (!_suppress && _samplerCombo.SelectedItem is string sm) { cfg.Sampler = sm; _vm.Save(); }
            };
            fetchSamplersBtn.Click += async (s, ev) =>
            {
                fetchSamplersBtn.IsEnabled = false;
                var samplers = await _vm.FetchSamplersAsync();
                if (samplers.Count > 0)
                {
                    _suppress = true;
                    _samplerCombo.Items.Clear();
                    foreach (var sm in samplers) _samplerCombo.Items.Add(sm);
                    var idx2 = samplers.IndexOf(cfg.Sampler ?? "");
                    _samplerCombo.SelectedIndex = idx2 >= 0 ? idx2 : 0;
                    if (_samplerCombo.SelectedItem is string sel2) cfg.Sampler = sel2;
                    _suppress = false;
                    _vm.Save();
                }
                fetchSamplersBtn.IsEnabled = true;
            };
            _samplerSection.Children.Add(_samplerCombo);
            paramStack.Children.Add(_samplerSection);

            // Steps
            _stepsSection = new SpacedPanel { Spacing = 2, Margin = new Thickness(0, 0, 0, 8) };
            _stepsLabel = new TextBlock { Foreground = textSec, FontSize = 12 };
            _stepsSlider = new Slider
            {
                Minimum = 1, Maximum = 150,
                Value = cfg.Steps,
                StepFrequency = 1,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            UpdateStepsLabel();
            _stepsSlider.ValueChanged += (s, ev) =>
            {
                if (!_suppress) { cfg.Steps = (int)_stepsSlider.Value; UpdateStepsLabel(); _vm.Save(); }
            };
            _stepsSection.Children.Add(_stepsLabel);
            _stepsSection.Children.Add(_stepsSlider);
            paramStack.Children.Add(_stepsSection);

            // CFG Scale
            _cfgSection = new SpacedPanel { Spacing = 2, Margin = new Thickness(0, 0, 0, 8) };
            _cfgLabel = new TextBlock { Foreground = textSec, FontSize = 12 };
            _cfgSlider = new Slider
            {
                Minimum = 1, Maximum = 30,
                Value = cfg.CfgScale,
                StepFrequency = 0.5,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            UpdateCfgLabel();
            _cfgSlider.ValueChanged += (s, ev) =>
            {
                if (!_suppress) { cfg.CfgScale = (float)_cfgSlider.Value; UpdateCfgLabel(); _vm.Save(); }
            };
            _cfgSection.Children.Add(_cfgLabel);
            _cfgSection.Children.Add(_cfgSlider);
            paramStack.Children.Add(_cfgSection);

            // Seed
            _seedSection = new SpacedPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };
            _seedBox = MakeTextBox("Seed (-1 = random)", "-1", cfg.Seed.ToString(), textPri);
            _seedBox.LostFocus += (s, ev) =>
            {
                if (int.TryParse(_seedBox.Text.Trim(), out int sv)) { cfg.Seed = sv; _vm.Save(); }
            };
            _seedSection.Children.Add(MakeLabeledControl("Seed (-1 = random)", _seedBox, textSec));
            paramStack.Children.Add(_seedSection);

            // CLIP Skip
            _clipSkipSection = new SpacedPanel { Spacing = 2, Margin = new Thickness(0, 0, 0, 0) };
            _clipSkipLabel = new TextBlock { Foreground = textSec, FontSize = 12 };
            _clipSkipSlider = new Slider
            {
                Minimum = 1, Maximum = 12,
                Value = cfg.ClipSkip,
                StepFrequency = 1,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            UpdateClipSkipLabel();
            _clipSkipSlider.ValueChanged += (s, ev) =>
            {
                if (!_suppress) { cfg.ClipSkip = (int)_clipSkipSlider.Value; UpdateClipSkipLabel(); _vm.Save(); }
            };
            _clipSkipSection.Children.Add(_clipSkipLabel);
            _clipSkipSection.Children.Add(_clipSkipSlider);
            paramStack.Children.Add(_clipSkipSection);

            _settingsBody.Children.Add(paramCard);

            // ── RESOLUTION ────────────────────────────────────────────────────
            _settingsBody.Children.Add(MakeSectionLabel("RESOLUTION", accent));
            var resCard = MakeCard(cardBg);
            var resStack = (SpacedPanel)resCard.Child;

            _resolutionLabel = new TextBlock
            {
                Foreground = textSec,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8)
            };
            UpdateResolutionLabel();
            resStack.Children.Add(_resolutionLabel);

            var resPresets = new []
            {
                new { Label = "Portrait 512×768", W = 512, H = 768 },
                new { Label = "Landscape 768×512", W = 768, H = 512 },
                new { Label = "Square 512×512", W = 512, H = 512 },
                new { Label = "HD Portrait 768×1024", W = 768, H = 1024 },
                new { Label = "HD Landscape 1024×768", W = 1024, H = 768 },
                new { Label = "HD Square 1024×1024", W = 1024, H = 1024 },
            };

            var resRow1 = new SpacedPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 0, 0, 6) };
            var resRow2 = new SpacedPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

            for (int i = 0; i < resPresets.Length; i++)
            {
                var preset = resPresets[i];
                var btn = new Button
                {
                    Content = preset.Label,
                    FontSize = 12,
                    Padding = new Thickness(10, 6, 10, 6)
                };
                btn.Click += (s, ev) =>
                {
                    cfg.Width = preset.W;
                    cfg.Height = preset.H;
                    UpdateResolutionLabel();
                    _vm.Save();
                };
                if (i < 3) resRow1.Children.Add(btn);
                else resRow2.Children.Add(btn);
            }

            resStack.Children.Add(resRow1);
            resStack.Children.Add(resRow2);
            _settingsBody.Children.Add(resCard);

            // ── NEGATIVE PROMPT ───────────────────────────────────────────────
            _settingsBody.Children.Add(MakeSectionLabel("NEGATIVE PROMPT", accent));
            _negativePromptSection = new StackPanel();
            var negCard = MakeCardWith(_negativePromptSection, cardBg);

            _negativePromptBox = new TextBox
            {
                PlaceholderText = "bad quality, blurry...",
                Text = cfg.NegativePrompt ?? "",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 80,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Style = (Style)Application.Current.Resources["DarkTextBoxStyle"]
            };
            _negativePromptBox.LostFocus += (s, ev) => { cfg.NegativePrompt = _negativePromptBox.Text; _vm.Save(); };
            _negativePromptSection.Children.Add(_negativePromptBox);
            _settingsBody.Children.Add(negCard);

            // Apply current backend visibility
            ApplyBackendVisibility(cfg.ActiveBackend);
        }

        // ── Backend change ─────────────────────────────────────────────────────

        private void OnBackendChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppress) return;
            int idx = _backendCombo.SelectedIndex;
            if (idx < 0 || idx >= BackendKeys.Length) return;
            string key = BackendKeys[idx];
            _vm.Config.ActiveBackend = key;
            _vm.Save();
            ApplyBackendVisibility(key);
        }

        private void ApplyBackendVisibility(string backend)
        {
            bool isUrl     = backend == "SD_WEBUI" || backend == "COMFYUI";
            bool isApiKey  = backend == "DALLE" || backend == "STABILITY" || backend == "HUGGINGFACE" || backend == "POLLINATIONS";
            bool isSd      = backend == "SD_WEBUI" || backend == "COMFYUI";
            bool hasCfg    = backend == "SD_WEBUI" || backend == "COMFYUI" || backend == "STABILITY";
            bool hasNeg    = backend == "SD_WEBUI" || backend == "COMFYUI" || backend == "STABILITY" || backend == "HUGGINGFACE";

            // URL row visibility
            SetVis(_sdUrlBox?.Parent as FrameworkElement, backend == "SD_WEBUI");
            SetVis(_comfyUrlBox?.Parent as FrameworkElement, backend == "COMFYUI");
            SetVis(_testSection, isUrl);

            // Parent card visibility
            var urlCardParent = (_urlSection?.Parent as Border);
            if (urlCardParent != null) urlCardParent.Visibility = isUrl ? Visibility.Visible : Visibility.Collapsed;

            var apiCardParent = (_apiKeySection?.Parent as Border);
            if (apiCardParent != null) apiCardParent.Visibility = isApiKey ? Visibility.Visible : Visibility.Collapsed;

            // API key value for the active backend
            if (_apiKeyBox != null)
            {
                _suppress = true;
                _apiKeyBox.Password = backend == "DALLE" ? _vm.Config.DalleApiKey
                    : backend == "STABILITY" ? _vm.Config.StabilityApiKey
                    : backend == "HUGGINGFACE" ? _vm.Config.HuggingfaceApiKey
                    : backend == "POLLINATIONS" ? _vm.Config.PollinationsApiKey
                    : "";
                _suppress = false;
            }

            // Model section
            bool hasDalle       = backend == "DALLE";
            bool hasPollinations = backend == "POLLINATIONS";
            bool hasHf          = backend == "HUGGINGFACE";
            bool hasModel       = hasDalle || hasPollinations || hasHf;

            var modelCardParent = (_modelSection?.Parent as Border);
            if (modelCardParent != null) modelCardParent.Visibility = hasModel ? Visibility.Visible : Visibility.Collapsed;

            SetVis(_dalleModelCombo?.Parent as FrameworkElement, hasDalle);
            SetVis(_pollinationsModelCombo?.Parent as FrameworkElement, hasPollinations);
            SetVis(_hfModelBox?.Parent as FrameworkElement, hasHf);

            // Parameters
            SetVis(_sdModelSection, isSd);
            SetVis(_samplerSection, isSd);
            SetVis(_stepsSection, isSd);
            SetVis(_cfgSection, hasCfg);
            SetVis(_seedSection, isSd);
            SetVis(_clipSkipSection, isSd);

            // Negative prompt
            var negCardParent = (_negativePromptSection?.Parent as Border);
            if (negCardParent != null) negCardParent.Visibility = hasNeg ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnApiKeyLostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppress) return;
            var cfg = _vm.Config;
            string key = _apiKeyBox.Password;
            switch (cfg.ActiveBackend)
            {
                case "DALLE":       cfg.DalleApiKey         = key; break;
                case "STABILITY":   cfg.StabilityApiKey     = key; break;
                case "HUGGINGFACE": cfg.HuggingfaceApiKey   = key; break;
                case "POLLINATIONS": cfg.PollinationsApiKey = key; break;
            }
            _vm.Save();
        }

        // ── Label updaters ─────────────────────────────────────────────────────

        private void UpdateStepsLabel()    => _stepsLabel.Text    = $"Steps: {(int)_stepsSlider.Value}";
        private void UpdateCfgLabel()      => _cfgLabel.Text      = $"CFG Scale: {_cfgSlider.Value:F1}";
        private void UpdateClipSkipLabel() => _clipSkipLabel.Text = $"CLIP Skip: {(int)_clipSkipSlider.Value}";
        private void UpdateResolutionLabel()
        {
            if (_resolutionLabel != null && _vm.Config != null)
                _resolutionLabel.Text = $"Current: {_vm.Config.Width} × {_vm.Config.Height}";
        }

        // ── UI helpers ─────────────────────────────────────────────────────────

        private static void SetVis(FrameworkElement el, bool visible)
        {
            if (el != null) el.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private static Border MakeCard(Brush bg)
        {
            return new Border
            {
                Margin = new Thickness(12, 0, 12, 8),
                Padding = new Thickness(16, 12, 16, 12),
                CornerRadius = new CornerRadius(10),
                Background = bg,
                Child = new SpacedPanel { Spacing = 6 }
            };
        }

        private static Border MakeCardWith(Panel inner, Brush bg)
        {
            return new Border
            {
                Margin = new Thickness(12, 0, 12, 8),
                Padding = new Thickness(16, 12, 16, 12),
                CornerRadius = new CornerRadius(10),
                Background = bg,
                Child = inner
            };
        }

        private static TextBlock MakeSectionLabel(string text, SolidColorBrush accent)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = accent,
                Margin = new Thickness(16, 8, 16, 6)
            };
        }

        private static TextBox MakeTextBox(string header, string placeholder, string value, SolidColorBrush textPri)
        {
            return new TextBox
            {
                PlaceholderText = placeholder,
                Text = value ?? "",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Style = (Style)Application.Current.Resources["DarkTextBoxStyle"]
            };
        }

        private static SpacedPanel MakeLabeledControl(string labelText, Control control, SolidColorBrush textSec)
        {
            var sp = new SpacedPanel { Spacing = 4 };
            sp.Children.Add(new TextBlock
            {
                Text = labelText,
                FontSize = 12,
                Foreground = textSec
            });
            sp.Children.Add(control);
            return sp;
        }
    }
}
