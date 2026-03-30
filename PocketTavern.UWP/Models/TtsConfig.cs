namespace PocketTavern.UWP.Models
{
    public class TtsConfig
    {
        public bool Enabled { get; set; } = false;
        public string Provider { get; set; } = "system";   // "system" | "openai"
        public bool AutoPlay { get; set; } = true;
        public string OpenAiUrl { get; set; } = "";
        public string OpenAiKey { get; set; } = "";
        public string OpenAiVoice { get; set; } = "alloy";
        public string OpenAiModel { get; set; } = "tts-1";
        public float Speed { get; set; } = 1.0f;
        public string FilterMode { get; set; } = "all";    // "all" | "quotes_only" | "no_asterisks"
    }
}
