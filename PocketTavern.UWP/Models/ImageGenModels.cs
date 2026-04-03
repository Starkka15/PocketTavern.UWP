using System.Collections.Generic;

namespace PocketTavern.UWP.Models
{
    public class ForgeGenerationParams
    {
        public string Prompt { get; set; } = "";
        public string NegativePrompt { get; set; } = "blurry, low quality, distorted, deformed, bad anatomy";
        public int Width { get; set; } = 512;
        public int Height { get; set; } = 768;
        public int Steps { get; set; } = 20;
        public float CfgScale { get; set; } = 7f;
        public string Sampler { get; set; } = "Euler";
        public int Seed { get; set; } = -1;
        /// <summary>Base64-encoded source image for img2img. Null = txt2img.</summary>
        public string SourceImageBase64 { get; set; }
        /// <summary>How much to change the source image (0.0 = identical, 1.0 = ignore source).</summary>
        public float DenoisingStrength { get; set; } = 0.5f;
    }

    public abstract class GenerationState
    {
        public sealed class Idle : GenerationState { }
        public sealed class Starting : GenerationState { }
        public sealed class InProgress : GenerationState
        {
            public float Progress { get; set; }
            public float Eta { get; set; }
            public string PreviewImage { get; set; }
        }
        public sealed class Complete : GenerationState
        {
            public string ImageBase64 { get; set; }
        }
        public sealed class Error : GenerationState
        {
            public string Message { get; set; }
        }
    }


    public enum ImageGenBackendType
    {
        SdWebui,
        ComfyUI,
        Dalle,
        Stability,
        Pollinations,
        HuggingFace
    }

    public class ImageGenCapabilities
    {
        public bool SupportsSamplers { get; set; } = false;
        public bool SupportsSchedulers { get; set; } = false;
        public bool SupportsModels { get; set; } = false;
        public bool SupportsSteps { get; set; } = false;
        public bool SupportsCfgScale { get; set; } = false;
        public bool SupportsSeed { get; set; } = false;
        public bool SupportsNegativePrompt { get; set; } = false;
        public bool SupportsImg2Img { get; set; } = false;
        public bool SupportsClipSkip { get; set; } = false;
        public bool SupportsVae { get; set; } = false;
        public bool SupportsResolutionPresets { get; set; } = true;
        public bool SupportsProgress { get; set; } = false;
        public bool RequiresApiKey { get; set; } = false;
        public bool RequiresUrl { get; set; } = false;
    }

    public class ImageGenConfig
    {
        public bool   Enabled       { get; set; } = false;
        public string ActiveBackend { get; set; } = "SD_WEBUI";
        public string SdWebuiUrl { get; set; } = "";
        public string ComfyuiUrl { get; set; } = "";
        public string DalleApiKey { get; set; } = "";
        public string DalleModel { get; set; } = "dall-e-3";
        public string StabilityApiKey { get; set; } = "";
        public string PollinationsApiKey { get; set; } = "";
        public string PollinationsModel { get; set; } = "flux";
        public string HuggingfaceApiKey { get; set; } = "";
        public string HuggingfaceModel { get; set; } = "stabilityai/stable-diffusion-xl-base-1.0";
        public string SdModel { get; set; } = "";
        public string Sampler { get; set; } = "Euler";
        public string Scheduler { get; set; } = "";
        public int Steps { get; set; } = 20;
        public float CfgScale { get; set; } = 7f;
        public int Seed { get; set; } = -1;
        public string NegativePrompt { get; set; } = "blurry, low quality, distorted, deformed, bad anatomy";
        public int ClipSkip { get; set; } = 1;
        public int Width { get; set; } = 512;
        public int Height { get; set; } = 768;
    }
}
