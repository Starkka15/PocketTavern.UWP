using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.Services
{
    /// <summary>ComfyUI image generation backend.</summary>
    public class ComfyUiImageGenBackend : IImageGenBackend
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };
        private static readonly Random _rng = new Random();
        private readonly Func<ImageGenConfig> _getConfig;

        public ComfyUiImageGenBackend(Func<ImageGenConfig> getConfig)
        {
            _getConfig = getConfig;
        }

        public ImageGenBackendType Type => ImageGenBackendType.ComfyUI;

        public ImageGenCapabilities Capabilities => new ImageGenCapabilities
        {
            SupportsSamplers = true,
            SupportsSteps = true,
            SupportsCfgScale = true,
            SupportsSeed = true,
            SupportsNegativePrompt = true,
            SupportsResolutionPresets = true,
            SupportsProgress = true,
            RequiresUrl = true
        };

        private string BaseUrl => _getConfig().ComfyuiUrl.TrimEnd('/');

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_getConfig().ComfyuiUrl)) return false;
                var resp = await _http.GetAsync(BaseUrl + "/system_stats");
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<List<string>> GetSamplersAsync()
        {
            try
            {
                var resp = await _http.GetAsync(BaseUrl + "/object_info/KSampler");
                if (!resp.IsSuccessStatusCode)
                    return new List<string> { "euler", "euler_ancestral", "dpmpp_2m", "dpmpp_sde", "ddim" };
                var root = JObject.Parse(await resp.Content.ReadAsStringAsync());
                var names = root["KSampler"]?["input"]?["required"]?["sampler_name"]?[0]
                    ?.Values<string>().ToList();
                return names ?? new List<string> { "euler", "euler_ancestral", "dpmpp_2m", "dpmpp_sde", "ddim" };
            }
            catch { return new List<string> { "euler", "euler_ancestral", "dpmpp_2m", "dpmpp_sde", "ddim" }; }
        }

        public async Task<List<string>> GetSchedulersAsync()
        {
            try
            {
                var resp = await _http.GetAsync(BaseUrl + "/object_info/KSampler");
                if (!resp.IsSuccessStatusCode)
                    return new List<string> { "normal", "karras", "exponential", "sgm_uniform" };
                var root = JObject.Parse(await resp.Content.ReadAsStringAsync());
                var names = root["KSampler"]?["input"]?["required"]?["scheduler"]?[0]
                    ?.Values<string>().ToList();
                return names ?? new List<string> { "normal", "karras", "exponential", "sgm_uniform" };
            }
            catch { return new List<string> { "normal", "karras", "exponential", "sgm_uniform" }; }
        }

        public async Task<List<string>> GetModelsAsync()
        {
            try
            {
                var resp = await _http.GetAsync(BaseUrl + "/object_info/CheckpointLoaderSimple");
                if (!resp.IsSuccessStatusCode) return new List<string>();
                var root = JObject.Parse(await resp.Content.ReadAsStringAsync());
                var names = root["CheckpointLoaderSimple"]?["input"]?["required"]?["ckpt_name"]?[0]
                    ?.Values<string>().ToList();
                return names ?? new List<string>();
            }
            catch { return new List<string>(); }
        }

        public async Task GenerateAsync(ForgeGenerationParams parameters, IProgress<GenerationState> progress, CancellationToken ct = default)
        {
            progress?.Report(new GenerationState.Starting());
            try
            {
                if (string.IsNullOrWhiteSpace(_getConfig().ComfyuiUrl))
                {
                    progress?.Report(new GenerationState.Error { Message = "No ComfyUI URL configured" });
                    return;
                }

                var seed = parameters.Seed == -1 ? (long)_rng.Next(int.MaxValue) : (long)parameters.Seed;
                var config = _getConfig();
                var sampler = config.Sampler.ToLowerInvariant().Replace(" ", "_");

                var workflow = BuildDefaultWorkflow(
                    parameters.Prompt, parameters.NegativePrompt,
                    parameters.Width, parameters.Height,
                    parameters.Steps, parameters.CfgScale,
                    sampler, seed);

                var payload = new JObject { ["prompt"] = JObject.Parse(workflow) };
                var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/prompt");
                req.Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

                var resp = await _http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode)
                    throw new Exception($"HTTP {(int)resp.StatusCode}");

                var promptResp = JObject.Parse(await resp.Content.ReadAsStringAsync());
                var promptId = promptResp["prompt_id"]?.ToString()
                    ?? throw new Exception("No prompt_id returned");

                // Poll for completion
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(1000, ct);

                    var histResp = await _http.GetAsync(BaseUrl + "/history/" + promptId, ct);
                    if (!histResp.IsSuccessStatusCode) continue;

                    var histRoot = JObject.Parse(await histResp.Content.ReadAsStringAsync());
                    var promptHist = histRoot[promptId] as JObject;
                    if (promptHist == null) continue;

                    var outputs = promptHist["outputs"] as JObject;
                    var saveNode = outputs?["9"] as JObject;
                    var images = saveNode?["images"] as JArray;
                    var firstImage = images?.FirstOrDefault() as JObject;
                    if (firstImage == null) continue;

                    var filename = firstImage["filename"]?.ToString();
                    if (filename == null) continue;

                    var subfolder = firstImage["subfolder"]?.ToString() ?? "";
                    var imageType = firstImage["type"]?.ToString() ?? "output";
                    var imgUrl = $"{BaseUrl}/view?filename={filename}&subfolder={subfolder}&type={imageType}";

                    var imgResp = await _http.GetAsync(imgUrl, ct);
                    if (!imgResp.IsSuccessStatusCode) throw new Exception("Failed to download image");

                    var imageBytes = await imgResp.Content.ReadAsByteArrayAsync();
                    var base64 = Convert.ToBase64String(imageBytes);
                    progress?.Report(new GenerationState.Complete { ImageBase64 = base64 });
                    return;
                }
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
                await _http.PostAsync(BaseUrl + "/interrupt",
                    new StringContent("{}", Encoding.UTF8, "application/json"));
            }
            catch { }
        }

        private static string BuildDefaultWorkflow(
            string prompt, string negativePrompt,
            int width, int height, int steps, float cfgScale,
            string sampler, long seed)
        {
            var escapedPrompt = prompt.Replace("\\", "\\\\").Replace("\"", "\\\"");
            var escapedNeg = negativePrompt.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return $@"{{
    ""3"": {{
        ""class_type"": ""KSampler"",
        ""inputs"": {{
            ""seed"": {seed},
            ""steps"": {steps},
            ""cfg"": {cfgScale},
            ""sampler_name"": ""{sampler}"",
            ""scheduler"": ""normal"",
            ""denoise"": 1,
            ""model"": [""4"", 0],
            ""positive"": [""6"", 0],
            ""negative"": [""7"", 0],
            ""latent_image"": [""5"", 0]
        }}
    }},
    ""4"": {{
        ""class_type"": ""CheckpointLoaderSimple"",
        ""inputs"": {{ ""ckpt_name"": ""v1-5-pruned-emaonly.safetensors"" }}
    }},
    ""5"": {{
        ""class_type"": ""EmptyLatentImage"",
        ""inputs"": {{ ""width"": {width}, ""height"": {height}, ""batch_size"": 1 }}
    }},
    ""6"": {{
        ""class_type"": ""CLIPTextEncode"",
        ""inputs"": {{ ""text"": ""{escapedPrompt}"", ""clip"": [""4"", 1] }}
    }},
    ""7"": {{
        ""class_type"": ""CLIPTextEncode"",
        ""inputs"": {{ ""text"": ""{escapedNeg}"", ""clip"": [""4"", 1] }}
    }},
    ""8"": {{
        ""class_type"": ""VAEDecode"",
        ""inputs"": {{ ""samples"": [""3"", 0], ""vae"": [""4"", 2] }}
    }},
    ""9"": {{
        ""class_type"": ""SaveImage"",
        ""inputs"": {{ ""filename_prefix"": ""PocketTavern"", ""images"": [""8"", 0] }}
    }}
}}";
        }
    }
}
