using System;
using System.Collections.Generic;

namespace PocketTavern.UWP.Models
{
    public class PromptOrderEntry
    {
        public string Identifier { get; set; } = "";
        public bool Enabled { get; set; } = true;
    }

    public class ChatContext
    {
        public string CharacterName { get; set; } = "";
        public string CharacterDescription { get; set; } = "";
        public string CharacterPersonality { get; set; } = "";
        public string CharacterScenario { get; set; } = "";
        public string CharacterFirstMessage { get; set; } = "";
        public string CharacterMessageExamples { get; set; } = "";
        public string CharacterSystemPrompt { get; set; } = "";
        public string CharacterPostHistoryInstructions { get; set; } = "";

        public UserPersonaInfo UserPersona { get; set; } = new UserPersonaInfo();
        public AuthorsNote AuthorsNote { get; set; } = new AuthorsNote();
        public List<WorldInfoEntry> WorldInfoEntries { get; set; } = new List<WorldInfoEntry>();
        public WorldInfoSettings WorldInfoSettings { get; set; } = new WorldInfoSettings();

        public InstructTemplate InstructTemplate { get; set; }
        public ContextTemplate ContextTemplate { get; set; }
        public string SystemPromptPreset { get; set; } = "";

        public Dictionary<string, string> OaiPrompts { get; set; } = new Dictionary<string, string>();
        public List<OaiPromptOrderItem> OaiPromptOrder { get; set; } = new List<OaiPromptOrderItem>();

        public bool IsLoaded { get; set; } = false;
        public long LastModified { get; set; } = 0;
    }

    public class UserPersonaInfo
    {
        public string Name { get; set; } = "User";
        public string Description { get; set; } = "";
        public int Position { get; set; } = 0;
        public int Depth { get; set; } = 2;
        public int Role { get; set; } = 0;
    }

    public class AuthorsNote
    {
        public string Content { get; set; } = "";
        public int Interval { get; set; } = 1;
        public int Depth { get; set; } = 4;
        public int Position { get; set; } = 0;
        public int Role { get; set; } = 0;
    }

    public class WorldInfoEntry
    {
        public string Uid { get; set; } = "";
        public List<string> Keys { get; set; } = new List<string>();
        public List<string> SecondaryKeys { get; set; } = new List<string>();
        public string Content { get; set; } = "";
        public string Comment { get; set; } = "";
        public bool Constant { get; set; } = false;
        public bool Selective { get; set; } = false;
        public int Order { get; set; } = 100;
        public int Position { get; set; } = 0;
        public int Depth { get; set; } = 4;
        public int Probability { get; set; } = 100;
        public bool Enabled { get; set; } = true;
        public string Group { get; set; } = "";
        public int? ScanDepth { get; set; }
        public bool CaseSensitive { get; set; } = false;
        public bool MatchWholeWords { get; set; } = false;
    }

    public class WorldInfoSettings
    {
        public int Depth { get; set; } = 2;
        public int Budget { get; set; } = 25;
        public int BudgetCap { get; set; } = 0;
        public int MinActivations { get; set; } = 0;
        public bool Recursive { get; set; } = false;
        public bool CaseSensitive { get; set; } = false;
        public bool MatchWholeWords { get; set; } = false;
    }

    public class PromptMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }

        public PromptMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }
}
