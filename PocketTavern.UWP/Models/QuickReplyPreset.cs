using System;
using System.Collections.Generic;

namespace PocketTavern.UWP.Models
{
    public class QuickReplyButton
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Label { get; set; } = "";
        public string Message { get; set; } = "";
        public HashSet<string> AutoTriggers { get; set; } = new HashSet<string>();
        public string Action { get; set; } = "";
    }

    public class QuickReplyPreset
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public List<QuickReplyButton> Buttons { get; set; } = new List<QuickReplyButton>();
    }
}
