using System;
using System.Collections.Generic;

namespace PocketTavern.UWP.Models
{
    public class OaiPromptOrderItem
    {
        public string Id { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public string CustomLabel { get; set; }
        public string Content { get; set; }
        public string Role { get; set; } = "system";
        public int InjectionPosition { get; set; } = 0;
        public int InjectionDepth { get; set; } = 4;

        private static readonly HashSet<string> MarkerIds = new HashSet<string>
        {
            "world_info_before", "world_info_after", "persona_description",
            "char_description", "char_personality", "scenario",
            "chat_examples", "chat_history"
        };

        public bool IsMarker => MarkerIds.Contains(Id);
        public bool IsCustom => Id.StartsWith("custom_");
        public string Label => CustomLabel ?? IdToLabel(Id);

        public static OaiPromptOrderItem Custom(string label, string content) => new OaiPromptOrderItem
        {
            Id = "custom_" + Guid.NewGuid(),
            Enabled = true,
            CustomLabel = label,
            Content = content
        };

        public static List<OaiPromptOrderItem> DefaultOrder() => new List<OaiPromptOrderItem>
        {
            new OaiPromptOrderItem { Id = "main_prompt", Content = "" },
            new OaiPromptOrderItem { Id = "world_info_before" },
            new OaiPromptOrderItem { Id = "persona_description" },
            new OaiPromptOrderItem { Id = "char_description" },
            new OaiPromptOrderItem { Id = "char_personality" },
            new OaiPromptOrderItem { Id = "scenario" },
            new OaiPromptOrderItem { Id = "auxiliary_prompt", Content = "" },
            new OaiPromptOrderItem { Id = "world_info_after" },
            new OaiPromptOrderItem { Id = "chat_examples" },
            new OaiPromptOrderItem { Id = "chat_history" },
            new OaiPromptOrderItem { Id = "post_history_instructions", Content = "" },
        };

        private static string IdToLabel(string id)
        {
            switch (id)
            {
                case "main_prompt":              return "Main Prompt";
                case "world_info_before":        return "World Info (before)";
                case "persona_description":      return "Persona Description";
                case "char_description":         return "Char Description";
                case "char_personality":         return "Char Personality";
                case "scenario":                 return "Scenario";
                case "auxiliary_prompt":         return "Auxiliary Prompt";
                case "world_info_after":         return "World Info (after)";
                case "chat_examples":            return "Chat Examples";
                case "chat_history":             return "Chat History";
                case "post_history_instructions":return "Post-History Instructions";
                default:                         return id;
            }
        }
    }
}
