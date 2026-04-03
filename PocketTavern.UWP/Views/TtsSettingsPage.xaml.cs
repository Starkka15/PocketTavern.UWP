using System.Collections.Generic;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.Controls;
using PocketTavern.UWP.Services;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class TtsSettingsPage : Page
    {
        private readonly TtsSettingsViewModel _vm = new TtsSettingsViewModel();
        private bool _suppress;

        // Dynamic sections
        private ToggleSwitch _enableToggle;
        private StackPanel _providerSection;
        private Button _systemBtn;
        private Button _openAiBtn;
        private SpacedPanel _openAiSection;
        private TextBox _apiUrlBox;
        private PasswordBox _apiKeyBox;
        private TextBox _modelBox;
        private ComboBox _voiceCombo;
        private List<TtsVoice> _fetchedVoices = new List<TtsVoice>();
        private Button _testVoiceBtn;
        private Button _stopVoiceBtn;
        private SpacedPanel _playbackSection;
        private ToggleSwitch _autoPlayToggle;
        private Slider _speedSlider;
        private TextBlock _speedLabel;
        private SpacedPanel _filterSection;
        private RadioButton _filterAll;
        private RadioButton _filterQuotes;
        private RadioButton _filterNoAsterisks;

        public TtsSettingsPage() { this.InitializeComponent(); }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _vm.Load();
            BuildContent();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _vm.StopVoice();
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

            // ── ENABLE ────────────────────────────────────────────────────────
            var enableCard = MakeCard(cardBg);
            var enableStack = (Panel)enableCard.Child;

            var enableRow = new Grid();
            enableRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new Windows.UI.Xaml.GridLength(1, Windows.UI.Xaml.GridUnitType.Star) });
            enableRow.ColumnDefinitions.Add(new ColumnDefinition { Width = Windows.UI.Xaml.GridLength.Auto });

            var enableInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            enableInfo.Children.Add(new TextBlock { Text = "Text-to-Speech", FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = textPri });
            enableInfo.Children.Add(new TextBlock { Text = "Speak AI messages aloud", FontSize = 12, Foreground = textSec, Margin = new Thickness(0, 2, 0, 0) });
            enableRow.Children.Add(enableInfo);

            _enableToggle = new ToggleSwitch
            {
                IsOn = cfg.Enabled,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, -8, 0)
            };
            _enableToggle.Toggled += OnEnabledToggled;
            Grid.SetColumn(_enableToggle, 1);
            enableRow.Children.Add(_enableToggle);

            enableStack.Children.Add(enableRow);
            ContentPanel.Children.Add(enableCard);

            // ── PROVIDER ──────────────────────────────────────────────────────
            ContentPanel.Children.Add(MakeSectionLabel("PROVIDER", accent));
            _providerSection = new StackPanel();
            var providerCard = MakeCardWith(_providerSection, cardBg);

            var providerRow = new SpacedPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

            _systemBtn = new Button { Content = "System TTS", Padding = new Thickness(14, 8, 14, 8) };
            _systemBtn.Click += (s, ev) => SetProvider("system");
            providerRow.Children.Add(_systemBtn);

            _openAiBtn = new Button { Content = "OpenAI-Compatible", Padding = new Thickness(14, 8, 14, 8) };
            _openAiBtn.Click += (s, ev) => SetProvider("openai");
            providerRow.Children.Add(_openAiBtn);

            _providerSection.Children.Add(providerRow);
            ContentPanel.Children.Add(providerCard);

            // ── OPENAI SETTINGS ───────────────────────────────────────────────
            ContentPanel.Children.Add(MakeSectionLabel("OPENAI-COMPATIBLE SETTINGS", accent));
            _openAiSection = new SpacedPanel { Spacing = 10 };
            var openAiCard = MakeCardWith(_openAiSection, cardBg);

            _openAiSection.Children.Add(new TextBlock
            {
                Text = "Works with OpenAI, AllTalk, XTTS, Kokoro, and other compatible endpoints",
                FontSize = 12,
                Foreground = textSec,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            });

            _apiUrlBox = new TextBox
            {
                PlaceholderText = "http://192.168.1.100:8000",
                Text = cfg.OpenAiUrl ?? "",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Style = (Style)Application.Current.Resources["DarkTextBoxStyle"]
            };
            _apiUrlBox.LostFocus += (s, ev) => { cfg.OpenAiUrl = _apiUrlBox.Text.Trim(); _vm.Save(); };
            _openAiSection.Children.Add(MakeLabeledControl("API URL", _apiUrlBox, textSec));

            _apiKeyBox = new PasswordBox
            {
                PlaceholderText = "API Key (optional)",
                Password = cfg.OpenAiKey ?? "",
                Margin = new Thickness(0, 4, 0, 0)
            };
            _apiKeyBox.LostFocus += (s, ev) => { cfg.OpenAiKey = _apiKeyBox.Password; _vm.Save(); };
            _openAiSection.Children.Add(MakeLabeledControl("API Key (optional)", _apiKeyBox, textSec));

            _modelBox = new TextBox
            {
                PlaceholderText = "tts-1",
                Text = cfg.OpenAiModel ?? "",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Style = (Style)Application.Current.Resources["DarkTextBoxStyle"]
            };
            _modelBox.LostFocus += (s, ev) => { cfg.OpenAiModel = _modelBox.Text.Trim(); _vm.Save(); };
            _openAiSection.Children.Add(MakeLabeledControl("Model", _modelBox, textSec));

            // Voice row: label + Refresh button
            var voiceHeader = new Grid();
            voiceHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new Windows.UI.Xaml.GridLength(1, Windows.UI.Xaml.GridUnitType.Star) });
            voiceHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = Windows.UI.Xaml.GridLength.Auto });
            voiceHeader.Children.Add(new TextBlock { Text = "Voice", FontSize = 12, Foreground = textSec, VerticalAlignment = VerticalAlignment.Center });
            var refreshVoicesBtn = new Button { Content = "\uE72C", FontFamily = new FontFamily("Segoe MDL2 Assets"), Padding = new Thickness(10, 4, 10, 4), FontSize = 14, Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0) };
            Grid.SetColumn(refreshVoicesBtn, 1);
            voiceHeader.Children.Add(refreshVoicesBtn);

            _voiceCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            refreshVoicesBtn.Click += async (s, ev) =>
            {
                refreshVoicesBtn.IsEnabled = false;
                await RefreshVoicesAsync();
                refreshVoicesBtn.IsEnabled = true;
            };
            _voiceCombo.SelectionChanged += (s, ev) =>
            {
                if (_suppress || _voiceCombo.SelectedIndex < 0 || _voiceCombo.SelectedIndex >= _fetchedVoices.Count) return;
                cfg.OpenAiVoice = _fetchedVoices[_voiceCombo.SelectedIndex].Id;
                _vm.Save();
            };

            var voiceContainer = new SpacedPanel { Spacing = 4 };
            voiceContainer.Children.Add(voiceHeader);
            voiceContainer.Children.Add(_voiceCombo);
            _openAiSection.Children.Add(voiceContainer);

            // Test Voice / Stop row
            var testRow = new SpacedPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
            _testVoiceBtn = new Button { Content = "Test Voice", Padding = new Thickness(14, 8, 14, 8) };
            _testVoiceBtn.Click += (s, ev) =>
            {
                var voiceId = _fetchedVoices.Count > 0 && _voiceCombo.SelectedIndex >= 0
                    ? _fetchedVoices[_voiceCombo.SelectedIndex].Id
                    : cfg.OpenAiVoice ?? "alloy";
                _vm.TestVoice(voiceId);
                _testVoiceBtn.Visibility = Visibility.Collapsed;
                _stopVoiceBtn.Visibility = Visibility.Visible;
            };
            _stopVoiceBtn = new Button { Content = "Stop", Padding = new Thickness(14, 8, 14, 8), Visibility = Visibility.Collapsed };
            _stopVoiceBtn.Click += (s, ev) =>
            {
                _vm.StopVoice();
                _stopVoiceBtn.Visibility = Visibility.Collapsed;
                _testVoiceBtn.Visibility = Visibility.Visible;
            };
            testRow.Children.Add(_testVoiceBtn);
            testRow.Children.Add(_stopVoiceBtn);
            _openAiSection.Children.Add(testRow);

            // Populate voice combo with defaults; will be refreshed when URL is set
            PopulateVoiceCombo(TtsSettingsViewModel.GetDefaultVoiceList(), cfg.OpenAiVoice);

            ContentPanel.Children.Add(openAiCard);

            // ── PLAYBACK ──────────────────────────────────────────────────────
            ContentPanel.Children.Add(MakeSectionLabel("PLAYBACK", accent));
            _playbackSection = new SpacedPanel { Spacing = 6 };
            var playbackCard = MakeCardWith(_playbackSection, cardBg);

            var autoPlayRow = new Grid();
            autoPlayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new Windows.UI.Xaml.GridLength(1, Windows.UI.Xaml.GridUnitType.Star) });
            autoPlayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = Windows.UI.Xaml.GridLength.Auto });

            var autoPlayInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            autoPlayInfo.Children.Add(new TextBlock { Text = "Auto-play", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = textPri });
            autoPlayInfo.Children.Add(new TextBlock { Text = "Automatically speak new AI messages", FontSize = 12, Foreground = textSec, Margin = new Thickness(0, 2, 0, 0) });
            autoPlayRow.Children.Add(autoPlayInfo);

            _autoPlayToggle = new ToggleSwitch
            {
                IsOn = cfg.AutoPlay,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, -8, 0)
            };
            _autoPlayToggle.Toggled += (s, ev) => { cfg.AutoPlay = _autoPlayToggle.IsOn; _vm.Save(); };
            Grid.SetColumn(_autoPlayToggle, 1);
            autoPlayRow.Children.Add(_autoPlayToggle);
            _playbackSection.Children.Add(autoPlayRow);

            // Speed slider
            var speedContainer = new SpacedPanel { Spacing = 2, Margin = new Thickness(0, 8, 0, 0) };
            _speedLabel = new TextBlock { Foreground = textSec, FontSize = 12 };
            _speedSlider = new Slider
            {
                Minimum = 0.5, Maximum = 2.0,
                Value = cfg.Speed,
                StepFrequency = 0.1,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            UpdateSpeedLabel();
            _speedSlider.ValueChanged += (s, ev) =>
            {
                if (!_suppress) { cfg.Speed = (float)_speedSlider.Value; UpdateSpeedLabel(); _vm.Save(); }
            };
            speedContainer.Children.Add(_speedLabel);
            speedContainer.Children.Add(_speedSlider);
            _playbackSection.Children.Add(speedContainer);

            ContentPanel.Children.Add(playbackCard);

            // ── TEXT FILTER ───────────────────────────────────────────────────
            ContentPanel.Children.Add(MakeSectionLabel("TEXT FILTER", accent));
            _filterSection = new SpacedPanel { Spacing = 0 };
            var filterCard = MakeCardWith(_filterSection, cardBg);

            _filterAll = new RadioButton
            {
                Content = "All text",
                GroupName = "FilterMode",
                IsChecked = cfg.FilterMode == "all",
                Margin = new Thickness(0, 4, 0, 4),
                Foreground = textPri
            };
            _filterAll.Checked += (s, ev) => { cfg.FilterMode = "all"; _vm.Save(); };
            _filterSection.Children.Add(_filterAll);

            // Separator
            _filterSection.Children.Add(new Windows.UI.Xaml.Shapes.Rectangle
            {
                Height = 1,
                Fill = surfaceBg,
                Margin = new Thickness(0, 2, 0, 2)
            });

            var quotesContent = new StackPanel();
            quotesContent.Children.Add(new TextBlock { Text = "Quotes only", FontSize = 14, Foreground = textPri });
            quotesContent.Children.Add(new TextBlock { Text = "(\"dialogue in quotes\")", FontSize = 12, Foreground = textSec, Margin = new Thickness(0, 1, 0, 0) });
            _filterQuotes = new RadioButton
            {
                Content = quotesContent,
                GroupName = "FilterMode",
                IsChecked = cfg.FilterMode == "quotes_only",
                Margin = new Thickness(0, 4, 0, 4),
                Foreground = textPri
            };
            _filterQuotes.Checked += (s, ev) => { cfg.FilterMode = "quotes_only"; _vm.Save(); };
            _filterSection.Children.Add(_filterQuotes);

            _filterSection.Children.Add(new Windows.UI.Xaml.Shapes.Rectangle
            {
                Height = 1,
                Fill = surfaceBg,
                Margin = new Thickness(0, 2, 0, 2)
            });

            var noAstContent = new StackPanel();
            noAstContent.Children.Add(new TextBlock { Text = "No action text", FontSize = 14, Foreground = textPri });
            noAstContent.Children.Add(new TextBlock { Text = "(*actions in asterisks* removed)", FontSize = 12, Foreground = textSec, Margin = new Thickness(0, 1, 0, 0) });
            _filterNoAsterisks = new RadioButton
            {
                Content = noAstContent,
                GroupName = "FilterMode",
                IsChecked = cfg.FilterMode == "no_asterisks",
                Margin = new Thickness(0, 4, 0, 4),
                Foreground = textPri
            };
            _filterNoAsterisks.Checked += (s, ev) => { cfg.FilterMode = "no_asterisks"; _vm.Save(); };
            _filterSection.Children.Add(_filterNoAsterisks);

            ContentPanel.Children.Add(filterCard);

            // Apply current enabled state (show/hide conditional sections)
            ApplyEnabledVisibility(cfg.Enabled);
            ApplyProviderStyle(cfg.Provider);
        }

        // ── Voice helpers ──────────────────────────────────────────────────────

        private async System.Threading.Tasks.Task RefreshVoicesAsync()
        {
            var voices = await _vm.GetVoicesAsync();
            PopulateVoiceCombo(voices, _vm.Config?.OpenAiVoice);
        }

        private void PopulateVoiceCombo(List<TtsVoice> voices, string selectedId)
        {
            _suppress = true;
            _fetchedVoices = voices;
            _voiceCombo.Items.Clear();
            int selIdx = 0;
            for (int i = 0; i < voices.Count; i++)
            {
                _voiceCombo.Items.Add(voices[i].Name);
                if (voices[i].Id == selectedId) selIdx = i;
            }
            if (voices.Count > 0) _voiceCombo.SelectedIndex = selIdx;
            _suppress = false;
        }

        // ── Toggle handlers ────────────────────────────────────────────────────

        private void OnEnabledToggled(object sender, RoutedEventArgs e)
        {
            if (_suppress) return;
            _vm.Config.Enabled = _enableToggle.IsOn;
            _vm.Save();
            ApplyEnabledVisibility(_enableToggle.IsOn);
        }

        private void SetProvider(string provider)
        {
            _vm.Config.Provider = provider;
            _vm.Save();
            ApplyProviderStyle(provider);
            ApplyOpenAiVisibility(provider == "openai");
        }

        private void ApplyEnabledVisibility(bool enabled)
        {
            SetVis(_providerSection?.Parent as FrameworkElement, enabled);
            // find the section label above _providerSection — just control the card and label via parent borders
            SetVis(_openAiSection?.Parent as FrameworkElement, enabled && _vm.Config.Provider == "openai");
            SetVis(_playbackSection?.Parent as FrameworkElement, enabled);
            SetVis(_filterSection?.Parent as FrameworkElement, enabled);

            // Also toggle section labels - they are in ContentPanel at fixed positions, easier to just rebuild is too heavy.
            // Instead we rely on card visibility. Labels always show. This is fine (labels without cards look minimal).
        }

        private void ApplyOpenAiVisibility(bool show)
        {
            SetVis(_openAiSection?.Parent as FrameworkElement, show);
        }

        private void ApplyProviderStyle(string provider)
        {
            var accent   = (SolidColorBrush)Application.Current.Resources["AccentPrimaryBrush"];
            var cardBg   = (SolidColorBrush)Application.Current.Resources["BackgroundCardBrush"];
            var textPri  = (SolidColorBrush)Application.Current.Resources["TextPrimaryBrush"];
            var transparent = new SolidColorBrush(Colors.Transparent);

            if (_systemBtn != null)
            {
                bool isSystem = provider == "system";
                _systemBtn.BorderBrush     = isSystem ? accent : cardBg;
                _systemBtn.BorderThickness = new Thickness(isSystem ? 2 : 1);
                _systemBtn.Foreground      = isSystem ? accent : textPri;
            }
            if (_openAiBtn != null)
            {
                bool isOai = provider == "openai";
                _openAiBtn.BorderBrush     = isOai ? accent : cardBg;
                _openAiBtn.BorderThickness = new Thickness(isOai ? 2 : 1);
                _openAiBtn.Foreground      = isOai ? accent : textPri;
            }

            ApplyOpenAiVisibility(provider == "openai");
        }

        // ── Label updaters ─────────────────────────────────────────────────────

        private void UpdateSpeedLabel()
        {
            if (_speedLabel != null && _speedSlider != null)
                _speedLabel.Text = $"Speed: {_speedSlider.Value:F1}x";
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
