using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class ApiConfigPage : Page
    {
        private readonly SettingsViewModel _vm = new SettingsViewModel();
        private bool _apiKeyVisible = false;
        private bool _suppressModelComboChanged = false;

        private static readonly string[] TextGenTypeValues =
        {
            "koboldcpp", "llamacpp", "ooba", "vllm", "aphrodite", "tabby",
            "ollama", "togetherai", "infermaticai", "openrouter",
            "featherless", "mancer", "dreamgen", "huggingface", "generic"
        };

        private static readonly string[] ProviderValues =
        {
            "openai", "claude", "openrouter", "nanogpt", "deepseek", "mistralai",
            "cohere", "perplexity", "groq", "makersuite", "vertexai", "ai21",
            "xai", "fireworks", "moonshot", "aimlapi", "pollinations", "chutes",
            "electronhub", "siliconflow", "zai", "azure_openai", "custom"
        };

        public ApiConfigPage() { this.InitializeComponent(); }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _vm.Load();
            PopulateControls();
            await FetchAndDisplayModelsAsync();
        }

        private void PopulateControls()
        {
            MainApiCombo.SelectedIndex = _vm.MainApi == "openai" ? 1 : 0;
            ApplyMainApiVisibility(_vm.MainApi);

            TextGenTypeCombo.SelectedIndex = IndexOf(TextGenTypeValues, _vm.TextGenType);
            ApiServerBox.Text = _vm.ApiServer ?? "";
            LocalModelBox.Text = _vm.CurrentModel ?? "";

            ProviderCombo.SelectedIndex = IndexOf(ProviderValues, _vm.ChatCompletionSource);
            CustomUrlBox.Text = _vm.CustomUrl ?? "";
            ModelBox.Text = _vm.CurrentModel ?? "";
            ApplyProviderVisibility(_vm.ChatCompletionSource);

            ApiKeyBox.Password = _vm.ApiKey ?? "";
            ApiKeyBoxVisible.Text = _vm.ApiKey ?? "";
        }

        private async System.Threading.Tasks.Task FetchAndDisplayModelsAsync()
        {
            SetModelsLoading(true);
            ReadControlsToVm();
            await _vm.FetchModelsAsync();
            SetModelsLoading(false);

            _suppressModelComboChanged = true;

            bool isLocal = _vm.MainApi != "openai";
            var models = _vm.AvailableModels;

            if (models.Count > 0)
            {
                var current = _vm.CurrentModel ?? "";

                if (isLocal)
                {
                    LocalModelCombo.Items.Clear();
                    foreach (var m in models) LocalModelCombo.Items.Add(m.Id);
                    LocalModelCombo.SelectedItem = current;
                    if (LocalModelCombo.SelectedIndex < 0 && LocalModelCombo.Items.Count > 0)
                        LocalModelCombo.SelectedIndex = 0;
                    LocalModelCombo.Visibility = Visibility.Visible;
                    LocalModelBox.Visibility = Visibility.Collapsed;
                    LocalModelCountLabel.Text = $"{models.Count} model{(models.Count == 1 ? "" : "s")} available";
                    LocalModelCountLabel.Visibility = Visibility.Visible;
                }
                else
                {
                    ModelCombo.Items.Clear();
                    foreach (var m in models) ModelCombo.Items.Add(m.Id);
                    ModelCombo.SelectedItem = current;
                    if (ModelCombo.SelectedIndex < 0 && ModelCombo.Items.Count > 0)
                        ModelCombo.SelectedIndex = 0;
                    ModelCombo.Visibility = Visibility.Visible;
                    ModelBox.Visibility = Visibility.Collapsed;
                    ModelCountLabel.Text = $"{models.Count} model{(models.Count == 1 ? "" : "s")} available";
                    ModelCountLabel.Visibility = Visibility.Visible;
                }
            }
            else
            {
                // No models — show text fields
                ModelCombo.Visibility      = Visibility.Collapsed;
                ModelBox.Visibility        = Visibility.Visible;
                ModelCountLabel.Visibility = Visibility.Collapsed;

                LocalModelCombo.Visibility      = Visibility.Collapsed;
                LocalModelBox.Visibility        = Visibility.Visible;
                LocalModelCountLabel.Visibility = Visibility.Collapsed;
            }

            _suppressModelComboChanged = false;
        }

        private void SetModelsLoading(bool loading)
        {
            ModelsLoadingRing.IsActive      = loading;
            LocalModelsLoadingRing.IsActive = loading;
            RefreshIcon.Visibility      = loading ? Visibility.Collapsed : Visibility.Visible;
            LocalRefreshIcon.Visibility = loading ? Visibility.Collapsed : Visibility.Visible;
            RefreshModelsButton.IsEnabled      = !loading;
            LocalRefreshButton.IsEnabled       = !loading;
        }

        private void OnMainApiChanged(object sender, SelectionChangedEventArgs e)
        {
            string mainApi = MainApiCombo.SelectedIndex == 1 ? "openai" : "textgenerationwebui";
            ApplyMainApiVisibility(mainApi);
        }

        private void ApplyMainApiVisibility(string mainApi)
        {
            bool isChat = mainApi == "openai";
            TextGenSection.Visibility = isChat ? Visibility.Collapsed : Visibility.Visible;
            ChatSection.Visibility    = isChat ? Visibility.Visible   : Visibility.Collapsed;
        }

        private void OnProviderChanged(object sender, SelectionChangedEventArgs e)
        {
            int idx = ProviderCombo.SelectedIndex;
            string provider = (idx >= 0 && idx < ProviderValues.Length) ? ProviderValues[idx] : "";
            ApplyProviderVisibility(provider);
        }

        private void ApplyProviderVisibility(string provider)
        {
            CustomUrlRow.Visibility = provider == "custom" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnModelComboChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressModelComboChanged) return;
            if (ModelCombo.SelectedItem is string selected)
            {
                ModelBox.Text = selected;
                _vm.CurrentModel = selected;
            }
        }

        private void OnLocalModelComboChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressModelComboChanged) return;
            if (LocalModelCombo.SelectedItem is string selected)
            {
                LocalModelBox.Text = selected;
                _vm.CurrentModel = selected;
            }
        }

        private async void OnRefreshModelsClick(object sender, RoutedEventArgs e)
        {
            ReadControlsToVm();
            await FetchAndDisplayModelsAsync();
        }

        private void OnToggleApiKeyVisibility(object sender, RoutedEventArgs e)
        {
            _apiKeyVisible = !_apiKeyVisible;
            if (_apiKeyVisible)
            {
                ApiKeyBoxVisible.Text = ApiKeyBox.Password;
                ApiKeyBox.Visibility = Visibility.Collapsed;
                ApiKeyBoxVisible.Visibility = Visibility.Visible;
                EyeIcon.Text = "\uED1A";
            }
            else
            {
                ApiKeyBox.Password = ApiKeyBoxVisible.Text;
                ApiKeyBoxVisible.Visibility = Visibility.Collapsed;
                ApiKeyBox.Visibility = Visibility.Visible;
                EyeIcon.Text = "\uE7B3";
            }
        }

        private void ReadControlsToVm()
        {
            _vm.MainApi = MainApiCombo.SelectedIndex == 1 ? "openai" : "textgenerationwebui";

            int tgIdx = TextGenTypeCombo.SelectedIndex;
            _vm.TextGenType = (tgIdx >= 0 && tgIdx < TextGenTypeValues.Length)
                ? TextGenTypeValues[tgIdx] : "koboldcpp";

            _vm.ApiServer = ApiServerBox.Text.Trim();

            int pvIdx = ProviderCombo.SelectedIndex;
            _vm.ChatCompletionSource = (pvIdx >= 0 && pvIdx < ProviderValues.Length)
                ? ProviderValues[pvIdx] : "openai";

            _vm.CustomUrl = CustomUrlBox.Text.Trim();
            _vm.ApiKey = _apiKeyVisible ? ApiKeyBoxVisible.Text : ApiKeyBox.Password;

            // Pick model from whichever control is visible
            if (_vm.MainApi == "openai")
                _vm.CurrentModel = ModelCombo.Visibility == Visibility.Visible
                    ? (ModelCombo.SelectedItem as string ?? ModelBox.Text.Trim())
                    : ModelBox.Text.Trim();
            else
                _vm.CurrentModel = LocalModelCombo.Visibility == Visibility.Visible
                    ? (LocalModelCombo.SelectedItem as string ?? LocalModelBox.Text.Trim())
                    : LocalModelBox.Text.Trim();
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            ReadControlsToVm();
            _vm.Save();
            App.Navigation.GoBack();
        }

        private async void OnTestConnectionClick(object sender, RoutedEventArgs e)
        {
            ReadControlsToVm();
            _vm.Save();
            TestButton.IsEnabled = false;
            ConnectionStatusLabel.Text = "Testing...";
            await _vm.TestConnectionAsync();
            ConnectionStatusLabel.Text = _vm.ConnectionStatusText;
            TestButton.IsEnabled = true;
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
            => App.Navigation.GoBack();

        private static int IndexOf(string[] arr, string value)
        {
            if (value == null) return 0;
            for (int i = 0; i < arr.Length; i++)
                if (arr[i] == value) return i;
            return 0;
        }
    }
}
