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
    /// <summary>HuggingFace Inference API image generation backend.</summary>
    public class HuggingFaceImageGenBackend : IImageGenBackend
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(180) };
        private readonly Func<ImageGenConfig> _getConfig;

        public HuggingFaceImageGenBackend(Func<ImageGenConfig> getConfig)
        {
            _getConfig = getConfig;
        }

        public ImageGenBackendType Type => ImageGenBackendType.HuggingFace;

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
            if (string.IsNullOrWhiteSpace(config.HuggingfaceApiKey)) return false;
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, "https://huggingface.co/api/whoami-v2");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.HuggingfaceApiKey);
                var resp = await _http.SendAsync(req);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public Task<List<string>> GetSamplersAsync() => Task.FromResult(new List<string>());
        public Task<List<string>> GetSchedulersAsync() => Task.FromResult(new List<string>());
        public Task<List<string>> GetModelsAsync() => Task.FromResult(new List<string>
        {
            "stabilityai/stable-diffusion-xl-base-1.0",
            "runwayml/stable-diffusion-v1-5",
            "CompVis/stable-diffusion-v1-4",
            "stabilityai/stable-diffusion-2-1"
        });

        public async Task GenerateAsync(ForgeGenerationParams parameters, IProgress<GenerationState> progress, CancellationToken ct = default)
        {
            progress?.Report(new GenerationState.Starting());
            try
            {
                var config = _getConfig();
                if (string.IsNullOrWhiteSpace(config.HuggingfaceApiKey))
                {
                    progress?.Report(new GenerationState.Error { Message = "No HuggingFace API key configured" });
                    return;
                }

                var hfParams = new JObject
                {
                    ["width"] = parameters.Width,
                    ["height"] = parameters.Height,
                    ["num_inference_steps"] = parameters.Steps,
                    ["guidance_scale"] = parameters.CfgScale
                };
                if (!string.IsNullOrWhiteSpace(parameters.NegativePrompt))
                    hfParams["negative_prompt"] = parameters.NegativePrompt;
                if (parameters.Seed != -1)
                    hfParams["seed"] = parameters.Seed;

                var body = new JObject
                {
                    ["inputs"] = parameters.Prompt,
                    ["parameters"] = hfParams
                };

                var modelId = !string.IsNullOrWhiteSpace(config.HuggingfaceModel)
                    ? config.HuggingfaceModel
                    : "stabilityai/stable-diffusion-xl-base-1.0";

                var req = new HttpRequestMessage(HttpMethod.Post,
                    $"https://api-inference.huggingface.co/models/{modelId}");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.HuggingfaceApiKey);
                req.Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");

                var resp = await _http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    throw new Exception(err);
                }

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
