using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.Services
{
    /// <summary>Pollinations image generation backend (free, no account required).</summary>
    public class PollinationsImageGenBackend : IImageGenBackend
    {
        private const string BaseUrl = "https://image.pollinations.ai/prompt";
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        private readonly Func<ImageGenConfig> _getConfig;

        public PollinationsImageGenBackend(Func<ImageGenConfig> getConfig)
        {
            _getConfig = getConfig;
        }

        public ImageGenBackendType Type => ImageGenBackendType.Pollinations;

        public ImageGenCapabilities Capabilities => new ImageGenCapabilities
        {
            SupportsResolutionPresets = true,
            SupportsNegativePrompt = true,
            SupportsSeed = true,
            SupportsModels = true,
            RequiresApiKey = false
        };

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var resp = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Head,
                    "https://image.pollinations.ai/prompt/test?width=64&height=64&nologo=true"));
                return resp.IsSuccessStatusCode || (int)resp.StatusCode == 302;
            }
            catch { return false; }
        }

        public Task<List<string>> GetSamplersAsync() => Task.FromResult(new List<string>());
        public Task<List<string>> GetSchedulersAsync() => Task.FromResult(new List<string>());
        public Task<List<string>> GetModelsAsync() => Task.FromResult(new List<string>
        {
            "flux", "flux-realism", "flux-anime", "flux-3d", "flux-cablyai", "turbo"
        });

        public async Task GenerateAsync(ForgeGenerationParams parameters, IProgress<GenerationState> progress, CancellationToken ct = default)
        {
            progress?.Report(new GenerationState.Starting());
            try
            {
                var config = _getConfig();
                var encodedPrompt = Uri.EscapeDataString(parameters.Prompt);
                var url = new StringBuilder($"{BaseUrl}/{encodedPrompt}");
                url.Append($"?width={parameters.Width}");
                url.Append($"&height={parameters.Height}");
                url.Append("&nologo=true");

                var model = !string.IsNullOrWhiteSpace(config.PollinationsModel) ? config.PollinationsModel : "flux";
                url.Append($"&model={model}");

                if (parameters.Seed != -1)
                    url.Append($"&seed={parameters.Seed}");
                if (!string.IsNullOrWhiteSpace(parameters.NegativePrompt))
                    url.Append($"&negative={Uri.EscapeDataString(parameters.NegativePrompt)}");
                if (!string.IsNullOrWhiteSpace(config.PollinationsApiKey))
                    url.Append($"&key={config.PollinationsApiKey}");

                progress?.Report(new GenerationState.InProgress { Progress = 0f, Eta = 0f });

                var req = new HttpRequestMessage(HttpMethod.Get, url.ToString());
                req.Headers.TryAddWithoutValidation("User-Agent", "PocketTavern");

                var resp = await _http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode)
                    throw new Exception($"HTTP {(int)resp.StatusCode}: {resp.ReasonPhrase}");

                var imageBytes = await resp.Content.ReadAsByteArrayAsync();
                var base64 = Convert.ToBase64String(imageBytes);
                progress?.Report(new GenerationState.Complete { ImageBase64 = base64 });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                progress?.Report(new GenerationState.Error { Message = ex.Message });
            }
        }

        public Task InterruptAsync() => Task.CompletedTask;
    }
}
