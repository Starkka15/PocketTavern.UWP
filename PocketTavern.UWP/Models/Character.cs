using System.Collections.Generic;

namespace PocketTavern.UWP.Models
{
    public class Character
    {
        public string Name { get; set; } = "";
        public string Avatar { get; set; }          // filename e.g. "aria.png"
        public string Description { get; set; } = "";
        public string Personality { get; set; } = "";
        public string Scenario { get; set; } = "";
        public string FirstMessage { get; set; } = "";
        public string MessageExample { get; set; } = "";
        public string CreatorNotes { get; set; } = "";
        public string SystemPrompt { get; set; } = "";
        public List<string> Tags { get; set; } = new List<string>();
        public List<string> AlternateGreetings { get; set; } = new List<string>();
        public string AttachedWorldInfo { get; set; }
        public bool HasCharacterBook { get; set; } = false;
        public int CharacterBookEntryCount { get; set; } = 0;
        public string PostHistoryInstructions { get; set; } = "";
        public string DepthPrompt { get; set; } = "";
        public int DepthPromptDepth { get; set; } = 4;
        public string DepthPromptRole { get; set; } = "system";
        public float Talkativeness { get; set; } = 0.5f;
        public bool IsFavorite { get; set; } = false;
        public bool UseAvatarForImageGen { get; set; } = true;
    }
}
