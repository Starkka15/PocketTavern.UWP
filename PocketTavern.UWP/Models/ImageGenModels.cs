namespace PocketTavern.UWP.Models
{
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
        public string ActiveBackend { get; set; } = "SdWebui";
        public string SdWebuiUrl { get; set; } = "";
        public string ComfyuiUrl { get; set; } = "";
        public string DalleApiKey { get; set; } = "";
        public string DalleModel { get; set; } = "dall-e-3";
        public string StabilityApiKey { get; set; } = "";
        public string PollinationsApiKey { get; set; } = "";
        public string PollinationsModel { get; set; } = "flux";
        public string HuggingfaceApiKey { get; set; } = "";
        public string HuggingfaceModel { get; set; } = "stabilityai/stable-diffusion-xl-base-1.0";
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
