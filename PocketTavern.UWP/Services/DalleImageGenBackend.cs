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
    /// <summary>DALL-E (OpenAI) image generation backend.</summary>
    public class DalleImageGenBackend : IImageGenBackend
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        private readonly Func<ImageGenConfig> _getConfig;

        public DalleImageGenBackend(Func<ImageGenConfig> getConfig)
        {
            _getConfig = getConfig;
        }

        public ImageGenBackendType Type => ImageGenBackendType.Dalle;

        public ImageGenCapabilities Capabilities => new ImageGenCapabilities
        {
            SupportsResolutionPresets = true,
            RequiresApiKey = true
        };

        public async Task<bool> TestConnectionAsync()
        {
            var config = _getConfig();
            if (string.IsNullOrWhiteSpace(config.DalleApiKey)) return false;
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.DalleApiKey);
                var resp = await _http.SendAsync(req);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public Task<List<string>> GetSamplersAsync() => Task.FromResult(new List<string>());
        public Task<List<string>> GetSchedulersAsync() => Task.FromResult(new List<string>());
        public Task<List<string>> GetModelsAsync() => Task.FromResult(new List<string> { "dall-e-3", "dall-e-2" });

        public async Task GenerateAsync(ForgeGenerationParams parameters, IProgress<GenerationState> progress, CancellationToken ct = default)
        {
            progress?.Report(new GenerationState.Starting());
            try
            {
                var config = _getConfig();
                if (string.IsNullOrWhiteSpace(config.DalleApiKey))
                {
                    progress?.Report(new GenerationState.Error { Message = "No OpenAI API key configured" });
                    return;
                }

                var size = PickDalleSize(config.DalleModel, parameters.Width, parameters.Height);
                var body = new JObject
                {
                    ["model"] = config.DalleModel,
                    ["prompt"] = parameters.Prompt,
                    ["n"] = 1,
                    ["size"] = size,
                    ["response_format"] = "b64_json"
                };

                var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/images/generations");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.DalleApiKey);
                req.Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");

                var resp = await _http.SendAsync(req, ct);
                var respBody = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    throw new Exception($"HTTP {(int)resp.StatusCode}: {respBody}");

                var json = JObject.Parse(respBody);
                var b64 = json["data"]?[0]?["b64_json"]?.ToString();
                if (b64 != null)
                    progress?.Report(new GenerationState.Complete { ImageBase64 = b64 });
                else
                    progress?.Report(new GenerationState.Error { Message = "No image returned" });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                progress?.Report(new GenerationState.Error { Message = ex.Message });
            }
        }

        public Task InterruptAsync() => Task.CompletedTask;

        private static string PickDalleSize(string model, int width, int height)
        {
            if (model == "dall-e-3")
            {
                if (width > height) return "1792x1024";
                if (height > width) return "1024x1792";
                return "1024x1024";
            }
            if (width <= 256 && height <= 256) return "256x256";
            if (width <= 512 && height <= 512) return "512x512";
            return "1024x1024";
        }
    }
}
