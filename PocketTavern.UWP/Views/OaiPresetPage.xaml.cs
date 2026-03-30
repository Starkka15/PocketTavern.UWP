using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class OaiPresetPage : Page
    {
        private readonly OaiPresetViewModel _vm = new OaiPresetViewModel();
        private bool _suppressPresetChanged = false;

        public OaiPresetPage() { this.InitializeComponent(); }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await _vm.LoadAsync();

            _suppressPresetChanged = true;
            PresetCombo.ItemsSource = _vm.PresetNames;
            PresetCombo.SelectedItem = _vm.SelectedPreset;
            PromptOrderList.ItemsSource = _vm.PromptOrderItems;
            _suppressPresetChanged = false;

            PopulateControls();
        }

        private void PopulateControls()
        {
            TempSlider.Value           = _vm.Temperature;
            TempEnabledCheck.IsChecked = _vm.TemperatureEnabled;
            TempLabel.Text             = _vm.Temperature.ToString("F2");

            TopPSlider.Value           = _vm.TopP;
            TopPEnabledCheck.IsChecked = _vm.TopPEnabled;
            TopPLabel.Text             = _vm.TopP.ToString("F2");

            MinPSlider.Value           = _vm.MinP;
            MinPEnabledCheck.IsChecked = _vm.MinPEnabled;
            MinPLabel.Text             = _vm.MinP.ToString("F2");

            TopASlider.Value           = _vm.TopA;
            TopAEnabledCheck.IsChecked = _vm.TopAEnabled;
            TopALabel.Text             = _vm.TopA.ToString("F2");

            TopKBox.Text               = _vm.TopK.ToString();
            TopKEnabledCheck.IsChecked = _vm.TopKEnabled;

            MaxTokensBox.Text               = _vm.MaxTokens.ToString();
            MaxTokensEnabledCheck.IsChecked = _vm.MaxTokensEnabled;

            FreqPenSlider.Value           = _vm.FrequencyPenalty;
            FreqPenEnabledCheck.IsChecked = _vm.FrequencyPenaltyEnabled;
            FreqPenLabel.Text             = _vm.FrequencyPenalty.ToString("F2");

            PresPenSlider.Value           = _vm.PresencePenalty;
            PresPenEnabledCheck.IsChecked = _vm.PresencePenaltyEnabled;
            PresPenLabel.Text             = _vm.PresencePenalty.ToString("F2");

            RepPenSlider.Value           = _vm.RepetitionPenalty;
            RepPenEnabledCheck.IsChecked = _vm.RepetitionPenaltyEnabled;
            RepPenLabel.Text             = _vm.RepetitionPenalty.ToString("F2");

            ContextSizeBox.Text               = _vm.ContextSize.ToString();
            ContextSizeEnabledCheck.IsChecked = _vm.ContextSizeEnabled;

            SeedBox.Text               = _vm.Seed.ToString();
            SeedEnabledCheck.IsChecked = _vm.SeedEnabled;
        }

        private void ReadControlsToVm()
        {
            _vm.Temperature        = (float)TempSlider.Value;
            _vm.TemperatureEnabled = TempEnabledCheck.IsChecked ?? true;

            _vm.TopP        = (float)TopPSlider.Value;
            _vm.TopPEnabled = TopPEnabledCheck.IsChecked ?? false;

            _vm.MinP        = (float)MinPSlider.Value;
            _vm.MinPEnabled = MinPEnabledCheck.IsChecked ?? false;

            _vm.TopA        = (float)TopASlider.Value;
            _vm.TopAEnabled = TopAEnabledCheck.IsChecked ?? false;

            if (int.TryParse(TopKBox.Text, out int tk)) _vm.TopK = tk;
            _vm.TopKEnabled = TopKEnabledCheck.IsChecked ?? false;

            if (int.TryParse(MaxTokensBox.Text, out int mt)) _vm.MaxTokens = mt;
            _vm.MaxTokensEnabled = MaxTokensEnabledCheck.IsChecked ?? true;

            _vm.FrequencyPenalty        = (float)FreqPenSlider.Value;
            _vm.FrequencyPenaltyEnabled = FreqPenEnabledCheck.IsChecked ?? false;

            _vm.PresencePenalty        = (float)PresPenSlider.Value;
            _vm.PresencePenaltyEnabled = PresPenEnabledCheck.IsChecked ?? false;

            _vm.RepetitionPenalty        = (float)RepPenSlider.Value;
            _vm.RepetitionPenaltyEnabled = RepPenEnabledCheck.IsChecked ?? false;

            if (int.TryParse(ContextSizeBox.Text, out int cs)) _vm.ContextSize = cs;
            _vm.ContextSizeEnabled = ContextSizeEnabledCheck.IsChecked ?? false;

            if (int.TryParse(SeedBox.Text, out int seed)) _vm.Seed = seed;
            _vm.SeedEnabled = SeedEnabledCheck.IsChecked ?? false;
        }

        // ── Preset selection ─────────────────────────────────────────────────────

        private async void OnPresetComboChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressPresetChanged) return;
            if (PresetCombo.SelectedItem is string name)
            {
                await _vm.SelectPresetAsync(name);
                PopulateControls();
                PromptOrderList.ItemsSource = null;
                PromptOrderList.ItemsSource = _vm.PromptOrderItems;
            }
        }

        // ── Slider label updates ─────────────────────────────────────────────────

        private void OnTempSliderChanged(object sender, RangeBaseValueChangedEventArgs e)
            => TempLabel.Text = e.NewValue.ToString("F2");
        private void OnTopPSliderChanged(object sender, RangeBaseValueChangedEventArgs e)
            => TopPLabel.Text = e.NewValue.ToString("F2");
        private void OnMinPSliderChanged(object sender, RangeBaseValueChangedEventArgs e)
            => MinPLabel.Text = e.NewValue.ToString("F2");
        private void OnTopASliderChanged(object sender, RangeBaseValueChangedEventArgs e)
            => TopALabel.Text = e.NewValue.ToString("F2");
        private void OnFreqPenSliderChanged(object sender, RangeBaseValueChangedEventArgs e)
            => FreqPenLabel.Text = e.NewValue.ToString("F2");
        private void OnPresPenSliderChanged(object sender, RangeBaseValueChangedEventArgs e)
            => PresPenLabel.Text = e.NewValue.ToString("F2");
        private void OnRepPenSliderChanged(object sender, RangeBaseValueChangedEventArgs e)
            => RepPenLabel.Text = e.NewValue.ToString("F2");

        // ── Prompt Order actions ─────────────────────────────────────────────────

        private void OnMoveUpClick(object sender, RoutedEventArgs e)
        {
            var item = (sender as FrameworkElement)?.Tag as OaiPromptOrderItemViewModel;
            if (item != null) _vm.MoveUp(item);
        }

        private void OnMoveDownClick(object sender, RoutedEventArgs e)
        {
            var item = (sender as FrameworkElement)?.Tag as OaiPromptOrderItemViewModel;
            if (item != null) _vm.MoveDown(item);
        }

        private void OnDeleteItemClick(object sender, RoutedEventArgs e)
        {
            var item = (sender as FrameworkElement)?.Tag as OaiPromptOrderItemViewModel;
            if (item != null) _vm.DeleteCustomItem(item);
        }

        private void OnExpandItemClick(object sender, RoutedEventArgs e)
        {
            var item = (sender as FrameworkElement)?.Tag as OaiPromptOrderItemViewModel;
            if (item != null) item.IsExpanded = !item.IsExpanded;
        }

        private async void OnAddCustomPromptClick(object sender, RoutedEventArgs e)
        {
            var nameBox = new TextBox
            {
                PlaceholderText = "e.g. NSFW Policy, Jailbreak…",
                Style = (Style)Application.Current.Resources["DarkTextBoxStyle"],
                Margin = new Thickness(0, 0, 0, 4)
            };
            var contentBox = new TextBox
            {
                PlaceholderText = "Enter prompt text. Use {{user}}, {{char}} macros.",
                AcceptsReturn = true,
                Height = 120,
                Style = (Style)Application.Current.Resources["DarkTextBoxStyle"]
            };
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = "Name",
                Style = (Style)Application.Current.Resources["SubtitleTextStyle"],
                Margin = new Thickness(0, 0, 0, 4)
            });
            panel.Children.Add(nameBox);
            panel.Children.Add(new TextBlock
            {
                Text = "Content",
                Style = (Style)Application.Current.Resources["SubtitleTextStyle"],
                Margin = new Thickness(0, 12, 0, 4)
            });
            panel.Children.Add(contentBox);

            var dialog = new ContentDialog
            {
                Title = "Add Custom Prompt",
                Content = panel,
                PrimaryButtonText = "Add",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                RequestedTheme = ElementTheme.Dark
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary
                && !string.IsNullOrWhiteSpace(nameBox.Text))
                _vm.AddCustomItem(nameBox.Text.Trim(), contentBox.Text);
        }

        // ── Save / Delete ────────────────────────────────────────────────────────

        private async void OnSaveClick(object sender, RoutedEventArgs e)
        {
            ReadControlsToVm();

            var nameBox = new TextBox
            {
                Text = _vm.Current.Name,
                PlaceholderText = "Preset name",
                Style = (Style)Application.Current.Resources["DarkTextBoxStyle"]
            };
            var dialog = new ContentDialog
            {
                Title = "Save Preset",
                Content = nameBox,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                RequestedTheme = ElementTheme.Dark
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary
                && !string.IsNullOrWhiteSpace(nameBox.Text))
            {
                await _vm.SaveAsync(nameBox.Text.Trim());
                _suppressPresetChanged = true;
                PresetCombo.ItemsSource = null;
                PresetCombo.ItemsSource = _vm.PresetNames;
                PresetCombo.SelectedItem = _vm.SelectedPreset;
                _suppressPresetChanged = false;
            }
        }

        private async void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Delete Preset",
                Content = $"Delete preset \"{_vm.Current.Name}\"? This cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                RequestedTheme = ElementTheme.Dark
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await _vm.DeleteAsync();
                _suppressPresetChanged = true;
                PresetCombo.ItemsSource = null;
                PresetCombo.ItemsSource = _vm.PresetNames;
                if (_vm.PresetNames.Count > 0)
                    PresetCombo.SelectedItem = _vm.SelectedPreset;
                PromptOrderList.ItemsSource = null;
                PromptOrderList.ItemsSource = _vm.PromptOrderItems;
                _suppressPresetChanged = false;
                PopulateControls();
            }
        }

        private void OnBackClick(object sender, RoutedEventArgs e) => App.Navigation.GoBack();
    }
}
