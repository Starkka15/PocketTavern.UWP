using System;

namespace PocketTavern.UWP.Models
{
    public class RegexRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public string Pattern { get; set; } = "";
        public bool IsRegex { get; set; } = false;
        public string Replacement { get; set; } = "";
        public bool ApplyToOutput { get; set; } = true;
        public bool ApplyToInput { get; set; } = false;
        public bool CaseInsensitive { get; set; } = false;
    }
}
