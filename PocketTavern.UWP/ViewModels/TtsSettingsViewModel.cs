using System.Collections.Generic;
using System.Threading.Tasks;
using PocketTavern.UWP.Models;
using PocketTavern.UWP.Services;

namespace PocketTavern.UWP.ViewModels
{
    public class TtsSettingsViewModel : ViewModelBase
    {
        private TtsConfig _config;
        private OpenAiTtsProvider _testProvider;

        public TtsConfig Config
        {
            get => _config;
            set => Set(ref _config, value);
        }

        public void Load()
        {
            Config = App.Settings.GetTtsConfig();
        }

        public void Save()
        {
            if (_config != null)
                App.Settings.SaveTtsConfig(_config);
        }

        public async Task<List<TtsVoice>> GetVoicesAsync()
        {
            if (_config == null || string.IsNullOrWhiteSpace(_config.OpenAiUrl))
                return DefaultVoices;
            var provider = new OpenAiTtsProvider
            {
                ApiUrl = _config.OpenAiUrl,
                ApiKey = _config.OpenAiKey ?? "",
                Model = _config.OpenAiModel ?? "tts-1"
            };
            var voices = await provider.GetVoicesAsync();
            return voices.Count > 0 ? voices : DefaultVoices;
        }

        public void TestVoice(string voiceId)
        {
            if (_config == null) return;
            StopVoice();
            _testProvider = new OpenAiTtsProvider
            {
                ApiUrl = _config.OpenAiUrl ?? "",
                ApiKey = _config.OpenAiKey ?? "",
                Model = _config.OpenAiModel ?? "tts-1"
            };
            var _ = _testProvider.SpeakAsync("This is a test of the text to speech system.", voiceId, (float)_config.Speed);
        }

        public void StopVoice()
        {
            try { _testProvider?.Stop(); } catch { }
            _testProvider = null;
        }

        public static List<TtsVoice> GetDefaultVoiceList() => new List<TtsVoice>(DefaultVoices);

        private static readonly List<TtsVoice> DefaultVoices = new List<TtsVoice>
        {
            new TtsVoice { Id = "alloy", Name = "Alloy" },
            new TtsVoice { Id = "echo",  Name = "Echo" },
            new TtsVoice { Id = "fable", Name = "Fable" },
            new TtsVoice { Id = "onyx",  Name = "Onyx" },
            new TtsVoice { Id = "nova",  Name = "Nova" },
            new TtsVoice { Id = "shimmer", Name = "Shimmer" }
        };
    }
}
