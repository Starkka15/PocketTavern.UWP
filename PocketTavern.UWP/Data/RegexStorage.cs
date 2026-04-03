using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using PocketTavern.UWP.Models;
using Windows.Storage;

namespace PocketTavern.UWP.Data
{
    /// <summary>
    /// Persists regex rules to LocalFolder/extensions/regex_rules.json.
    /// </summary>
    public class RegexStorage
    {
        private readonly string _filePath;

        public RegexStorage()
        {
            var dir = Path.Combine(ApplicationData.Current.LocalFolder.Path, "extensions");
            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "regex_rules.json");
        }

        public List<RegexRule> Load()
        {
            if (!File.Exists(_filePath))
                return new List<RegexRule>();
            try
            {
                return JsonConvert.DeserializeObject<List<RegexRule>>(File.ReadAllText(_filePath))
                       ?? new List<RegexRule>();
            }
            catch { return new List<RegexRule>(); }
        }

        public void Save(List<RegexRule> rules)
        {
            File.WriteAllText(_filePath, JsonConvert.SerializeObject(rules, Formatting.Indented));
        }
    }
}
