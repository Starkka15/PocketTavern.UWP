using System;
using System.Threading.Tasks;
using PocketTavern.UWP.Data;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.Services
{
    public class TtsVoice
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Language { get; set; } = "";
    }

    public class TtsManager
    {
        private SystemTtsProvider _systemProvider;
        private OpenAiTtsProvider _openAiProvider;
        private bool _speaking;

        public bool IsSpeaking => _speaking;

        public async Task SpeakAsync(string text, string characterFile)
        {
            var config = App.Settings.GetTtsConfig();
            if (!config.Enabled) return;

            var filteredText = TtsTextFilter.Filter(text, config.FilterMode);
            if (string.IsNullOrWhiteSpace(filteredText)) return;

            // Determine provider: per-character override or global
            var providerName = characterFile != null
                ? TtsVoiceStorage.GetProviderOverride(characterFile) ?? config.Provider
                : config.Provider;

            // Determine voice: per-character or global default
            var voiceId = characterFile != null
                ? TtsVoiceStorage.GetVoiceId(characterFile) : null;

            _speaking = true;
            try
            {
                switch (providerName)
                {
                    case "openai":
                        var openAi = GetOpenAiProvider();
                        openAi.ApiUrl = config.OpenAiUrl;
                        openAi.ApiKey = config.OpenAiKey ?? "";
                        openAi.Model = !string.IsNullOrWhiteSpace(config.OpenAiModel) ? config.OpenAiModel : "tts-1";
                        var voice = !string.IsNullOrWhiteSpace(voiceId) ? voiceId
                            : !string.IsNullOrWhiteSpace(config.OpenAiVoice) ? config.OpenAiVoice
                            : null;
                        await openAi.SpeakAsync(filteredText, voice, config.Speed);
                        break;
                    default:
                        var system = GetSystemProvider();
                        await system.SpeakAsync(filteredText, voiceId, config.Speed);
                        break;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[TtsManager] Speak failed: {ex.Message}");
            }
            finally
            {
                _speaking = false;
            }
        }

        public void Stop()
        {
            _systemProvider?.Stop();
            _openAiProvider?.Stop();
            _speaking = false;
        }

        public async Task<System.Collections.Generic.List<TtsVoice>> GetVoicesAsync()
        {
            var config = App.Settings.GetTtsConfig();
            return await GetVoicesForProviderAsync(config.Provider);
        }

        public async Task<System.Collections.Generic.List<TtsVoice>> GetVoicesForProviderAsync(string provider)
        {
            switch (provider)
            {
                case "openai":
                    var config = App.Settings.GetTtsConfig();
                    var p = GetOpenAiProvider();
                    p.ApiUrl = config.OpenAiUrl;
                    p.ApiKey = config.OpenAiKey;
                    p.Model = config.OpenAiModel;
                    return await p.GetVoicesAsync();
                default:
                    return await GetSystemProvider().GetVoicesAsync();
            }
        }

        public void Shutdown()
        {
            _systemProvider?.Shutdown();
            _openAiProvider?.Stop();
            _systemProvider = null;
            _openAiProvider = null;
        }

        private SystemTtsProvider GetSystemProvider()
            => _systemProvider ?? (_systemProvider = new SystemTtsProvider());

        private OpenAiTtsProvider GetOpenAiProvider()
            => _openAiProvider ?? (_openAiProvider = new OpenAiTtsProvider());
    }
}
