using System;
using System.Collections.Generic;
using System.Linq;
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
    /// <summary>
    /// Stable Diffusion WebUI / Forge image generation backend.
    /// Endpoint: /sdapi/v1/
    /// </summary>
    public class SdWebuiImageGenBackend : IImageGenBackend
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };
        private readonly Func<ImageGenConfig> _getConfig;

        public SdWebuiImageGenBackend(Func<ImageGenConfig> getConfig)
        {
            _getConfig = getConfig;
        }

        public ImageGenBackendType Type => ImageGenBackendType.SdWebui;

        public ImageGenCapabilities Capabilities => new ImageGenCapabilities
        {
            SupportsSamplers = true,
            SupportsSchedulers = false,
            SupportsModels = true,
            SupportsSteps = true,
            SupportsCfgScale = true,
            SupportsSeed = true,
            SupportsNegativePrompt = true,
            SupportsImg2Img = true,
            SupportsClipSkip = true,
            SupportsVae = true,
            SupportsResolutionPresets = true,
            SupportsProgress = true,
            RequiresUrl = true
        };

        private string BaseUrl => _getConfig().SdWebuiUrl.TrimEnd('/');

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var resp = await _http.GetAsync(BaseUrl + "/sdapi/v1/options");
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<List<string>> GetSamplersAsync()
        {
            try
            {
                var resp = await _http.GetAsync(BaseUrl + "/sdapi/v1/samplers");
                if (!resp.IsSuccessStatusCode) return new List<string>();
                var arr = JArray.Parse(await resp.Content.ReadAsStringAsync());
                return arr.Select(t => t["name"]?.ToString()).Where(n => n != null).ToList();
            }
            catch { return new List<string>(); }
        }

        public async Task<List<string>> GetSchedulersAsync()
        {
            return new List<string>();
        }

        public async Task<List<string>> GetModelsAsync()
        {
            try
            {
                var resp = await _http.GetAsync(BaseUrl + "/sdapi/v1/sd-models");
                if (!resp.IsSuccessStatusCode) return new List<string>();
                var arr = JArray.Parse(await resp.Content.ReadAsStringAsync());
                return arr.Select(t => t["title"]?.ToString()).Where(n => n != null).ToList();
            }
            catch { return new List<string>(); }
        }

        public async Task GenerateAsync(ForgeGenerationParams parameters, IProgress<GenerationState> progress, CancellationToken ct = default)
        {
            progress?.Report(new GenerationState.Starting());
            try
            {
                JObject requestBody;
                string endpoint;

                if (parameters.SourceImageBase64 != null)
                {
                    endpoint = "/sdapi/v1/img2img";
                    requestBody = new JObject
                    {
                        ["prompt"] = parameters.Prompt,
                        ["negative_prompt"] = parameters.NegativePrompt,
                        ["init_images"] = new JArray(parameters.SourceImageBase64),
                        ["steps"] = parameters.Steps,
                        ["cfg_scale"] = parameters.CfgScale,
                        ["width"] = parameters.Width,
                        ["height"] = parameters.Height,
                        ["sampler_name"] = parameters.Sampler,
                        ["denoising_strength"] = parameters.DenoisingStrength,
                        ["seed"] = parameters.Seed,
                        ["batch_size"] = 1,
                        ["send_images"] = true,
                        ["save_images"] = false
                    };
                }
                else
                {
                    endpoint = "/sdapi/v1/txt2img";
                    requestBody = new JObject
                    {
                        ["prompt"] = parameters.Prompt,
                        ["negative_prompt"] = parameters.NegativePrompt,
                        ["steps"] = parameters.Steps,
                        ["cfg_scale"] = parameters.CfgScale,
                        ["width"] = parameters.Width,
                        ["height"] = parameters.Height,
                        ["sampler_name"] = parameters.Sampler,
                        ["seed"] = parameters.Seed,
                        ["batch_size"] = 1,
                        ["send_images"] = true,
                        ["save_images"] = false
                    };
                }

                var content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");
                var resp = await _http.PostAsync(BaseUrl + endpoint, content, ct);
                if (!resp.IsSuccessStatusCode)
                    throw new Exception($"HTTP {(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");

                var body = JObject.Parse(await resp.Content.ReadAsStringAsync());
                var images = body["images"] as JArray;
                var image = images?.FirstOrDefault()?.ToString();
                if (image != null)
                    progress?.Report(new GenerationState.Complete { ImageBase64 = image });
                else
                    progress?.Report(new GenerationState.Error { Message = "No image generated" });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                progress?.Report(new GenerationState.Error { Message = ex.Message });
            }
        }

        public async Task InterruptAsync()
        {
            try
            {
                await _http.PostAsync(BaseUrl + "/sdapi/v1/interrupt",
                    new StringContent("{}", Encoding.UTF8, "application/json"));
            }
            catch { }
        }
    }
}
