using System.Collections.Generic;
using System.Linq;

namespace PocketTavern.UWP.Models
{
    public static class ActivationStrategy
    {
        public const int Natural = 0;
        public const int List = 1;
        public const int Manual = 2;
        public const int Pooled = 3;
    }

    public static class GenerationMode
    {
        public const int Swap = 0;
        public const int Append = 1;
    }

    public class Group
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public List<string> Members { get; set; } = new List<string>();
        public string ChatId { get; set; }
        public string Avatar { get; set; }
        public bool Favorite { get; set; } = false;
        public int ActivationStrategyValue { get; set; } = ActivationStrategy.Natural;
        public int GenerationModeValue { get; set; } = GenerationMode.Swap;
        public List<string> DisabledMembers { get; set; } = new List<string>();
        public bool AllowSelfResponses { get; set; } = false;
        public List<string> Chats { get; set; } = new List<string>();

        public List<string> EnabledMembers =>
            Members?.Where(m => !DisabledMembers.Contains(m)).ToList() ?? new List<string>();
    }

    public class WorldInfoListItem
    {
        public string FileId { get; set; } = "";
        public string Name { get; set; } = "";
    }
}
