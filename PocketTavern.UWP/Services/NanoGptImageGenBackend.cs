using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.Services
{
    public class NanoGptImageGenBackend : IImageGenBackend
    {
        private const string BaseUrl = "https://nano-gpt.com";
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        private readonly Func<ImageGenConfig> _getConfig;

        public NanoGptImageGenBackend(Func<ImageGenConfig> getConfig)
        {
            _getConfig = getConfig;
        }

        public ImageGenBackendType Type => ImageGenBackendType.NanoGpt;

        public ImageGenCapabilities Capabilities => new ImageGenCapabilities
        {
            SupportsModels = true,
            SupportsResolutionPresets = false,
            RequiresApiKey = true
        };

        public async Task<bool> TestConnectionAsync()
        {
            var config = _getConfig();
            if (string.IsNullOrWhiteSpace(config.NanoGptApiKey)) return false;
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/models");
                req.Headers.TryAddWithoutValidation("x-api-key", config.NanoGptApiKey);
                var resp = await _http.SendAsync(req);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public Task<List<string>> GetSamplersAsync() => Task.FromResult(new List<string>());
        public Task<List<string>> GetSchedulersAsync() => Task.FromResult(new List<string>());

        public async Task<List<string>> GetModelsAsync()
        {
            var config = _getConfig();
            if (string.IsNullOrWhiteSpace(config.NanoGptApiKey)) return new List<string>();
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/models");
                req.Headers.TryAddWithoutValidation("x-api-key", config.NanoGptApiKey);
                var resp = await _http.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return new List<string>();
                var body = await resp.Content.ReadAsStringAsync();
                var root = JObject.Parse(body);
                var imageSection = root["models"]?["image"] as JObject;
                if (imageSection == null) return new List<string>();
                var ids = new List<string>(imageSection.Count);
                foreach (var kv in imageSection) ids.Add(kv.Key);
                ids.Sort();
                return ids;
            }
            catch { return new List<string>(); }
        }

        public async Task GenerateAsync(ForgeGenerationParams parameters, IProgress<GenerationState> progress, CancellationToken ct = default)
        {
            progress?.Report(new GenerationState.Starting());
            try
            {
                var config = _getConfig();
                if (string.IsNullOrWhiteSpace(config.NanoGptApiKey))
                {
                    progress?.Report(new GenerationState.Error { Message = "No nano-gpt API key configured" });
                    return;
                }

                var model = !string.IsNullOrWhiteSpace(config.NanoGptModel) ? config.NanoGptModel : "chroma";
                var payload = JsonConvert.SerializeObject(new { prompt = parameters.Prompt, model, n = 1, response_format = "b64_json" });

                var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v1/images/generations");
                req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {config.NanoGptApiKey}");
                req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                progress?.Report(new GenerationState.InProgress { Progress = 0f, Eta = 0f });

                var resp = await _http.SendAsync(req, ct);
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    throw new Exception($"HTTP {(int)resp.StatusCode}: {body}");

                var root = JObject.Parse(body);
                var b64 = root["data"]?[0]?["b64_json"]?.ToString();
                if (!string.IsNullOrEmpty(b64))
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
    }
}
