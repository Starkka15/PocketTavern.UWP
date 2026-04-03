using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PocketTavern.UWP.Data;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.Services
{
    /// <summary>
    /// Orchestrates image generation backends. Matches Android's ImageGenRepository.
    /// </summary>
    public class ImageGenService
    {
        private readonly SettingsStorage _settings;
        private readonly Dictionary<ImageGenBackendType, IImageGenBackend> _backends;

        public ImageGenService(SettingsStorage settings)
        {
            _settings = settings;
            Func<ImageGenConfig> getConfig = () => _settings.GetImageGenConfig();
            _backends = new Dictionary<ImageGenBackendType, IImageGenBackend>
            {
                [ImageGenBackendType.SdWebui]    = new SdWebuiImageGenBackend(getConfig),
                [ImageGenBackendType.ComfyUI]    = new ComfyUiImageGenBackend(getConfig),
                [ImageGenBackendType.Dalle]      = new DalleImageGenBackend(getConfig),
                [ImageGenBackendType.Stability]  = new StabilityImageGenBackend(getConfig),
                [ImageGenBackendType.Pollinations] = new PollinationsImageGenBackend(getConfig),
                [ImageGenBackendType.HuggingFace] = new HuggingFaceImageGenBackend(getConfig)
            };
        }

        public IImageGenBackend GetActiveBackend()
        {
            var config = _settings.GetImageGenConfig();
            if (Enum.TryParse<ImageGenBackendType>(config.ActiveBackend, out var type)
                && _backends.TryGetValue(type, out var backend))
                return backend;
            return _backends[ImageGenBackendType.SdWebui];
        }

        public IImageGenBackend GetBackend(ImageGenBackendType type) =>
            _backends.TryGetValue(type, out var b) ? b : _backends[ImageGenBackendType.SdWebui];

        public Task GenerateAsync(ForgeGenerationParams parameters, IProgress<GenerationState> progress,
            CancellationToken ct = default) =>
            GetActiveBackend().GenerateAsync(parameters, progress, ct);

        public Task<bool> TestConnectionAsync() =>
            GetActiveBackend().TestConnectionAsync();

        public Task<List<string>> GetSamplersAsync() =>
            GetActiveBackend().GetSamplersAsync();

        public Task<List<string>> GetSchedulersAsync() =>
            GetActiveBackend().GetSchedulersAsync();

        public Task<List<string>> GetModelsAsync() =>
            GetActiveBackend().GetModelsAsync();

        public Task InterruptAsync() =>
            GetActiveBackend().InterruptAsync();

        /// <summary>Build generation params from the current config.</summary>
        public ForgeGenerationParams BuildParams(string prompt, string sourceImageBase64 = null)
        {
            var config = _settings.GetImageGenConfig();
            return new ForgeGenerationParams
            {
                Prompt = prompt,
                NegativePrompt = config.NegativePrompt,
                Width = config.Width,
                Height = config.Height,
                Steps = config.Steps,
                CfgScale = config.CfgScale,
                Sampler = config.Sampler,
                Seed = config.Seed,
                SourceImageBase64 = sourceImageBase64
            };
        }
    }
}
