using System.Collections.Generic;
using Newtonsoft.Json;

namespace PocketTavern.UWP.Models
{
    /// <summary>
    /// Character Card V2 format for PNG embedding.
    /// Full spec: https://github.com/malfoyslastname/character-card-spec-v2
    /// </summary>
    public class CharacterCardV2
    {
        [JsonProperty("spec")]
        public string Spec { get; set; } = "chara_card_v2";

        [JsonProperty("spec_version")]
        public string SpecVersion { get; set; } = "2.0";

        [JsonProperty("data")]
        public CharacterCardData Data { get; set; } = new CharacterCardData();
    }

    public class CharacterCardData
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        [JsonProperty("personality")]
        public string Personality { get; set; } = "";

        [JsonProperty("scenario")]
        public string Scenario { get; set; } = "";

        [JsonProperty("first_mes")]
        public string FirstMes { get; set; } = "";

        [JsonProperty("mes_example")]
        public string MesExample { get; set; } = "";

        [JsonProperty("creator_notes")]
        public string CreatorNotes { get; set; } = "";

        [JsonProperty("system_prompt")]
        public string SystemPrompt { get; set; } = "";

        [JsonProperty("post_history_instructions")]
        public string PostHistoryInstructions { get; set; } = "";

        [JsonProperty("alternate_greetings")]
        public List<string> AlternateGreetings { get; set; } = new List<string>();

        [JsonProperty("character_version")]
        public string CharacterVersion { get; set; } = "";

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new List<string>();

        [JsonProperty("creator")]
        public string Creator { get; set; } = "";

        [JsonProperty("character_book")]
        public CharacterBook CharacterBook { get; set; }
    }

    public class CharacterBook
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        [JsonProperty("entries")]
        public List<CharacterBookEntry> Entries { get; set; } = new List<CharacterBookEntry>();

        [JsonProperty("scan_depth")]
        public int? ScanDepth { get; set; }

        [JsonProperty("token_budget")]
        public int? TokenBudget { get; set; }

        [JsonProperty("recursive_scanning")]
        public bool RecursiveScanning { get; set; } = false;
    }

    public class CharacterBookEntry
    {
        [JsonProperty("id")]
        public int? Id { get; set; }

        [JsonProperty("keys")]
        public List<string> Keys { get; set; } = new List<string>();

        [JsonProperty("secondary_keys")]
        public List<string> SecondaryKeys { get; set; } = new List<string>();

        [JsonProperty("content")]
        public string Content { get; set; } = "";

        [JsonProperty("comment")]
        public string Comment { get; set; } = "";

        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty("constant")]
        public bool Constant { get; set; } = false;

        [JsonProperty("selective")]
        public bool Selective { get; set; } = false;

        [JsonProperty("insertion_order")]
        public int InsertionOrder { get; set; } = 100;

        [JsonProperty("priority")]
        public int? Priority { get; set; }

        [JsonProperty("position")]
        public string Position { get; set; } = "before_char";

        [JsonProperty("case_sensitive")]
        public bool CaseSensitive { get; set; } = false;
    }
}
