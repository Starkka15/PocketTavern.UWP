using System.Collections.Generic;
using Newtonsoft.Json;

namespace PocketTavern.UWP.Models
{
    public class FormattingSettings
    {
        public List<string> InstructPresets { get; set; } = new List<string>();
        public string SelectedInstructPreset { get; set; } = "";
        public List<string> ContextPresets { get; set; } = new List<string>();
        public string SelectedContextPreset { get; set; } = "";
        public List<string> SystemPromptPresets { get; set; } = new List<string>();
        public string SelectedSystemPromptPreset { get; set; } = "";
        public string CustomSystemPrompt { get; set; } = "";
    }

    public class InstructTemplate
    {
        public string Name { get; set; } = "";

        [JsonProperty("input_sequence")]       public string InputSequence { get; set; } = "";
        [JsonProperty("input_suffix")]         public string InputSuffix { get; set; } = "";
        [JsonProperty("output_sequence")]      public string OutputSequence { get; set; } = "";
        [JsonProperty("output_suffix")]        public string OutputSuffix { get; set; } = "";
        [JsonProperty("first_output_sequence")] public string FirstOutputSequence { get; set; } = "";
        [JsonProperty("last_output_sequence")] public string LastOutputSequence { get; set; } = "";
        [JsonProperty("system_sequence")]      public string SystemSequence { get; set; } = "";
        [JsonProperty("system_suffix")]        public string SystemSuffix { get; set; } = "";
        [JsonProperty("stop_sequence")]        public string StopSequence { get; set; } = "";
        [JsonProperty("separator_sequence")]   public string SeparatorSequence { get; set; } = "";
        [JsonProperty("system_prompt")]        public string SystemPrompt { get; set; } = "";
        [JsonProperty("wrap")]                 public bool Wrap { get; set; } = false;
    }

    public class ContextTemplate
    {
        public string Name { get; set; } = "";

        [JsonProperty("story_string")]      public string StoryString { get; set; } = "";
        [JsonProperty("chat_start")]        public string ChatStart { get; set; } = "";
        [JsonProperty("example_separator")] public string ExampleSeparator { get; set; } = "";
    }

    public class SystemPromptPreset
    {
        public string Name { get; set; } = "";
        public string Content { get; set; } = "";
    }
}
