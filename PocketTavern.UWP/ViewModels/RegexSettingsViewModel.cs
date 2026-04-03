using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PocketTavern.UWP.Data;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.ViewModels
{
    public class RegexSettingsViewModel : ViewModelBase
    {
        private readonly RegexStorage _storage = new RegexStorage();

        public ObservableCollection<RegexRule> Rules { get; }
            = new ObservableCollection<RegexRule>();

        public void Load()
        {
            Rules.Clear();
            foreach (var r in _storage.Load())
                Rules.Add(r);
        }

        public void AddRule(string name, string pattern, bool isRegex, string replacement,
            bool applyToOutput, bool applyToInput, bool caseInsensitive)
        {
            Rules.Add(new RegexRule
            {
                Name = name,
                Pattern = pattern,
                IsRegex = isRegex,
                Replacement = replacement,
                ApplyToOutput = applyToOutput,
                ApplyToInput = applyToInput,
                CaseInsensitive = caseInsensitive,
                Enabled = true
            });
            SaveAll();
        }

        public void UpdateRule(string id, string name, string pattern, bool isRegex, string replacement,
            bool applyToOutput, bool applyToInput, bool caseInsensitive)
        {
            var rule = Rules.FirstOrDefault(r => r.Id == id);
            if (rule == null) return;
            rule.Name = name;
            rule.Pattern = pattern;
            rule.IsRegex = isRegex;
            rule.Replacement = replacement;
            rule.ApplyToOutput = applyToOutput;
            rule.ApplyToInput = applyToInput;
            rule.CaseInsensitive = caseInsensitive;
            SaveAll();
        }

        public void ToggleRule(string id)
        {
            var rule = Rules.FirstOrDefault(r => r.Id == id);
            if (rule == null) return;
            rule.Enabled = !rule.Enabled;
            SaveAll();
        }

        public void DeleteRule(string id)
        {
            var rule = Rules.FirstOrDefault(r => r.Id == id);
            if (rule != null)
            {
                Rules.Remove(rule);
                SaveAll();
            }
        }

        private void SaveAll() => _storage.Save(Rules.ToList());
    }
}
