using Newtonsoft.Json;

namespace PocketTavern.UWP.Models
{
    public class TextGenPreset
    {
        public string Name { get; set; } = "";

        [JsonProperty("max_new_tokens")]   public int? MaxNewTokens { get; set; }
        [JsonProperty("min_length")]       public int MinTokens { get; set; } = 0;
        [JsonProperty("truncation_length")] public int TruncationLength { get; set; } = 2048;
        [JsonProperty("temp")]             public float Temperature { get; set; } = 0.7f;
        [JsonProperty("top_p")]            public float TopP { get; set; } = 0.5f;
        [JsonProperty("top_k")]            public int TopK { get; set; } = 40;
        [JsonProperty("top_a")]            public float TopA { get; set; } = 0f;
        [JsonProperty("min_p")]            public float MinP { get; set; } = 0f;
        [JsonProperty("typical_p")]        public float TypicalP { get; set; } = 1.0f;
        [JsonProperty("tfs")]              public float Tfs { get; set; } = 1.0f;
        [JsonProperty("rep_pen")]          public float RepPen { get; set; } = 1.2f;
        [JsonProperty("rep_pen_range")]    public int RepPenRange { get; set; } = 0;
        [JsonProperty("rep_pen_slope")]    public float RepPenSlope { get; set; } = 1f;
        [JsonProperty("freq_pen")]         public float FrequencyPenalty { get; set; } = 0f;
        [JsonProperty("presence_pen")]     public float PresencePenalty { get; set; } = 0f;
        [JsonProperty("dry_multiplier")]   public float DryMultiplier { get; set; } = 0f;
        [JsonProperty("dry_base")]         public float DryBase { get; set; } = 1.75f;
        [JsonProperty("dry_allowed_length")] public int DryAllowedLength { get; set; } = 2;
        [JsonProperty("dry_penalty_last_n")] public int DryPenaltyLastN { get; set; } = 0;
        [JsonProperty("mirostat_mode")]    public int MirostatMode { get; set; } = 0;
        [JsonProperty("mirostat_tau")]     public float MirostatTau { get; set; } = 5f;
        [JsonProperty("mirostat_eta")]     public float MirostatEta { get; set; } = 0.1f;
        [JsonProperty("xtc_threshold")]    public float XtcThreshold { get; set; } = 0.1f;
        [JsonProperty("xtc_probability")]  public float XtcProbability { get; set; } = 0f;
        [JsonProperty("skew")]             public float Skew { get; set; } = 0f;
        [JsonProperty("smoothing_factor")] public float SmoothingFactor { get; set; } = 0f;
        [JsonProperty("smoothing_curve")]  public float SmoothingCurve { get; set; } = 1f;
        [JsonProperty("guidance_scale")]   public float GuidanceScale { get; set; } = 1f;
        [JsonProperty("add_bos_token")]    public bool AddBosToken { get; set; } = true;
        [JsonProperty("ban_eos_token")]    public bool BanEosToken { get; set; } = false;
        [JsonProperty("skip_special_tokens")] public bool SkipSpecialTokens { get; set; } = true;
    }
}
