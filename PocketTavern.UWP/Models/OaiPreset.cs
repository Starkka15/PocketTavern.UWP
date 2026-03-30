using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace PocketTavern.UWP.Models
{
    public class OaiPreset
    {
        public string Name { get; set; } = "";

        public float Temperature { get; set; } = 1.0f;
        [JsonProperty("temperature_enabled")]   public bool TemperatureEnabled { get; set; } = true;

        [JsonProperty("top_p")]                 public float TopP { get; set; } = 1.0f;
        [JsonProperty("top_p_enabled")]         public bool TopPEnabled { get; set; } = false;

        [JsonProperty("top_k")]                 public int TopK { get; set; } = 0;
        [JsonProperty("top_k_enabled")]         public bool TopKEnabled { get; set; } = false;

        [JsonProperty("max_tokens")]            public int MaxTokens { get; set; } = 500;
        [JsonProperty("max_tokens_enabled")]    public bool MaxTokensEnabled { get; set; } = true;

        [JsonProperty("frequency_penalty")]         public float FrequencyPenalty { get; set; } = 0f;
        [JsonProperty("frequency_penalty_enabled")] public bool FrequencyPenaltyEnabled { get; set; } = false;

        [JsonProperty("presence_penalty")]          public float PresencePenalty { get; set; } = 0f;
        [JsonProperty("presence_penalty_enabled")]  public bool PresencePenaltyEnabled { get; set; } = false;

        [JsonProperty("repetition_penalty")]        public float RepetitionPenalty { get; set; } = 1.0f;
        [JsonProperty("repetition_penalty_enabled")] public bool RepetitionPenaltyEnabled { get; set; } = false;

        [JsonProperty("min_p")]                 public float MinP { get; set; } = 0f;
        [JsonProperty("min_p_enabled")]         public bool MinPEnabled { get; set; } = false;

        [JsonProperty("top_a")]                 public float TopA { get; set; } = 0f;
        [JsonProperty("top_a_enabled")]         public bool TopAEnabled { get; set; } = false;

        [JsonProperty("context_size")]          public int ContextSize { get; set; } = 4096;
        [JsonProperty("context_size_enabled")]  public bool ContextSizeEnabled { get; set; } = false;

        public int Seed { get; set; } = -1;
        [JsonProperty("seed_enabled")]          public bool SeedEnabled { get; set; } = false;

        [JsonProperty("prompt_order")]
        public List<OaiPromptOrderItem> PromptOrder { get; set; } = OaiPromptOrderItem.DefaultOrder();

        public string MainPromptContent =>
            PromptOrder?.FirstOrDefault(i => i.Id == "main_prompt" && i.Enabled)?.Content ?? "";
    }
}
