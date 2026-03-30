using System.Collections.ObjectModel;
using System.Threading.Tasks;
using PocketTavern.UWP.Models;
using PocketTavern.UWP.Services;

namespace PocketTavern.UWP.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {

        private string _mainApi = "textgenerationwebui";
        private string _apiServer = "http://127.0.0.1:5001";
        private string _textGenType = "koboldcpp";
        private string _chatCompletionSource = "openai";
        private string _customUrl = "";
        private string _apiKey = "";
        private string _currentModel = "";
        private bool _isConnecting = false;
        private string _connectionStatusText = "";
        private bool _isConnected = false;
        private bool _isLoadingModels = false;

        private readonly LlmService _llm = new LlmService();

        public ObservableCollection<AvailableModel> AvailableModels { get; } = new ObservableCollection<AvailableModel>();
        public bool IsLoadingModels { get => _isLoadingModels; set => Set(ref _isLoadingModels, value); }

        public string MainApi         { get => _mainApi; set => Set(ref _mainApi, value); }
        public string ApiServer       { get => _apiServer; set => Set(ref _apiServer, value); }
        public string TextGenType     { get => _textGenType; set => Set(ref _textGenType, value); }
        public string ChatCompletionSource { get => _chatCompletionSource; set => Set(ref _chatCompletionSource, value); }
        public string CustomUrl       { get => _customUrl; set => Set(ref _customUrl, value); }
        public string ApiKey          { get => _apiKey; set => Set(ref _apiKey, value); }
        public string CurrentModel    { get => _currentModel; set => Set(ref _currentModel, value); }
        public bool IsConnecting      { get => _isConnecting; set => Set(ref _isConnecting, value); }
        public string ConnectionStatusText { get => _connectionStatusText; set => Set(ref _connectionStatusText, value); }
        public bool IsConnected       { get => _isConnected; set => Set(ref _isConnected, value); }

        public bool UsesChatCompletion => string.Equals(MainApi, "openai", System.StringComparison.OrdinalIgnoreCase);

        public void Load()
        {
            var cfg = App.Settings.GetLlmConfig();
            MainApi = cfg.MainApi;
            ApiServer = cfg.ApiServer;
            TextGenType = cfg.TextGenType;
            ChatCompletionSource = cfg.ChatCompletionSource;
            CustomUrl = cfg.CustomUrl ?? "";
            ApiKey = cfg.ApiKey;
            CurrentModel = cfg.CurrentModel;
        }

        public void Save()
        {
            var cfg = new ApiConfiguration
            {
                MainApi = MainApi,
                ApiServer = ApiServer,
                TextGenType = TextGenType,
                ChatCompletionSource = ChatCompletionSource,
                CustomUrl = string.IsNullOrWhiteSpace(CustomUrl) ? null : CustomUrl,
                ApiKey = ApiKey,
                CurrentModel = CurrentModel
            };
            App.Settings.SaveLlmConfig(cfg);
        }

        public async Task FetchModelsAsync()
        {
            IsLoadingModels = true;
            var cfg = new ApiConfiguration
            {
                MainApi = MainApi,
                ApiServer = ApiServer,
                TextGenType = TextGenType,
                ChatCompletionSource = ChatCompletionSource,
                CustomUrl = string.IsNullOrWhiteSpace(CustomUrl) ? null : CustomUrl,
                ApiKey = ApiKey,
                CurrentModel = CurrentModel
            };
            var models = await _llm.GetAvailableModelsAsync(cfg);
            AvailableModels.Clear();
            foreach (var m in models) AvailableModels.Add(m);
            IsLoadingModels = false;
        }

        public async Task TestConnectionAsync()
        {
            Save();
            IsConnecting = true;
            ConnectionStatusText = "Testing...";

            var cfg = App.Settings.GetLlmConfig();
            var result = await _llm.TestConnectionAsync(cfg);

            IsConnected = result.Connected;
            ConnectionStatusText = result.Connected
                ? $"Connected — {(string.IsNullOrEmpty(result.Model) ? "OK" : result.Model)}"
                : $"Failed: {result.Error}";
            IsConnecting = false;
        }

        public void NavigateToConnectionProfiles() => App.Navigation.NavigateToConnectionProfiles();
        public void NavigateToTextGen()            => App.Navigation.NavigateToTextGen();
        public void NavigateToOaiPreset()          => App.Navigation.NavigateToOaiPreset();
        public void NavigateToFormatting()         => App.Navigation.NavigateToFormatting();
        public void NavigateToPersona()            => App.Navigation.NavigateToPersona();
        public void NavigateToWorldInfo()          => App.Navigation.NavigateToWorldInfo();
        public void NavigateToExtensions()         => App.Navigation.NavigateToExtensions();
    }
}
