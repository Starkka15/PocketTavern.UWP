using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.Services
{
    /// <summary>Stability AI image generation backend.</summary>
    public class StabilityImageGenBackend : IImageGenBackend
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        private readonly Func<ImageGenConfig> _getConfig;

        public StabilityImageGenBackend(Func<ImageGenConfig> getConfig)
        {
            _getConfig = getConfig;
        }

        public ImageGenBackendType Type => ImageGenBackendType.Stability;

        public ImageGenCapabilities Capabilities => new ImageGenCapabilities
        {
            SupportsSteps = true,
            SupportsCfgScale = true,
            SupportsSeed = true,
            SupportsNegativePrompt = true,
            SupportsResolutionPresets = true,
            RequiresApiKey = true
        };

        public async Task<bool> TestConnectionAsync()
        {
            var config = _getConfig();
            if (string.IsNullOrWhiteSpace(config.StabilityApiKey)) return false;
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, "https://api.stability.ai/v1/user/account");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.StabilityApiKey);
                var resp = await _http.SendAsync(req);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public Task<List<string>> GetSamplersAsync() => Task.FromResult(new List<string>());
        public Task<List<string>> GetSchedulersAsync() => Task.FromResult(new List<string>());
        public Task<List<string>> GetModelsAsync() => Task.FromResult(new List<string>
        {
            "stable-diffusion-xl-1024-v1-0",
            "stable-diffusion-v1-6"
        });

        public async Task GenerateAsync(ForgeGenerationParams parameters, IProgress<GenerationState> progress, CancellationToken ct = default)
        {
            progress?.Report(new GenerationState.Starting());
            try
            {
                var config = _getConfig();
                if (string.IsNullOrWhiteSpace(config.StabilityApiKey))
                {
                    progress?.Report(new GenerationState.Error { Message = "No Stability AI API key configured" });
                    return;
                }

                // Dimensions must be multiples of 64
                var width = (parameters.Width / 64) * 64;
                var height = (parameters.Height / 64) * 64;

                var prompts = new JArray(new JObject { ["text"] = parameters.Prompt, ["weight"] = 1.0f });
                if (!string.IsNullOrWhiteSpace(parameters.NegativePrompt))
                    prompts.Add(new JObject { ["text"] = parameters.NegativePrompt, ["weight"] = -1.0f });

                var body = new JObject
                {
                    ["text_prompts"] = prompts,
                    ["width"] = Math.Max(512, Math.Min(1024, width)),
                    ["height"] = Math.Max(512, Math.Min(1024, height)),
                    ["steps"] = parameters.Steps,
                    ["cfg_scale"] = parameters.CfgScale,
                    ["seed"] = parameters.Seed == -1 ? 0 : parameters.Seed,
                    ["samples"] = 1
                };

                const string engineId = "stable-diffusion-xl-1024-v1-0";
                var req = new HttpRequestMessage(HttpMethod.Post,
                    $"https://api.stability.ai/v1/generation/{engineId}/text-to-image");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.StabilityApiKey);
                req.Headers.Accept.ParseAdd("application/json");
                req.Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");

                var resp = await _http.SendAsync(req, ct);
                var respBody = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    throw new Exception($"HTTP {(int)resp.StatusCode}: {respBody}");

                var json = JObject.Parse(respBody);
                var b64 = json["artifacts"]?[0]?["base64"]?.ToString();
                if (b64 != null)
                    progress?.Report(new GenerationState.Complete { ImageBase64 = b64 });
                else
                    progress?.Report(new GenerationState.Error { Message = json["message"]?.ToString() ?? "No image returned" });
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
