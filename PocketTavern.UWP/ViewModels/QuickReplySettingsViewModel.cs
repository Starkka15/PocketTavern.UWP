using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PocketTavern.UWP.Data;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.ViewModels
{
    public class QuickReplySettingsViewModel : ViewModelBase
    {
        private readonly QuickReplyStorage _storage = new QuickReplyStorage();

        public ObservableCollection<QuickReplyPreset> Presets { get; }
            = new ObservableCollection<QuickReplyPreset>();

        public void Load()
        {
            Presets.Clear();
            foreach (var p in _storage.Load())
                Presets.Add(p);
        }

        public void AddPreset(string name)
        {
            Presets.Add(new QuickReplyPreset { Name = name, Enabled = true });
            SaveAll();
        }

        public void RenamePreset(string id, string name)
        {
            var preset = Presets.FirstOrDefault(p => p.Id == id);
            if (preset == null) return;
            preset.Name = name;
            SaveAll();
        }

        public void TogglePreset(string id)
        {
            var preset = Presets.FirstOrDefault(p => p.Id == id);
            if (preset == null) return;
            preset.Enabled = !preset.Enabled;
            SaveAll();
        }

        public void DeletePreset(string id)
        {
            var preset = Presets.FirstOrDefault(p => p.Id == id);
            if (preset != null)
            {
                Presets.Remove(preset);
                SaveAll();
            }
        }

        public void AddButton(string presetId, string label, string message, HashSet<string> autoTriggers)
        {
            var preset = Presets.FirstOrDefault(p => p.Id == presetId);
            if (preset == null) return;
            preset.Buttons.Add(new QuickReplyButton
            {
                Label = label,
                Message = message,
                AutoTriggers = autoTriggers ?? new HashSet<string>()
            });
            SaveAll();
        }

        public void UpdateButton(string presetId, string buttonId, string label, string message, HashSet<string> autoTriggers)
        {
            var preset = Presets.FirstOrDefault(p => p.Id == presetId);
            if (preset == null) return;
            var btn = preset.Buttons.FirstOrDefault(b => b.Id == buttonId);
            if (btn == null) return;
            btn.Label = label;
            btn.Message = message;
            btn.AutoTriggers = autoTriggers ?? new HashSet<string>();
            SaveAll();
        }

        public void DeleteButton(string presetId, string buttonId)
        {
            var preset = Presets.FirstOrDefault(p => p.Id == presetId);
            if (preset == null) return;
            var btn = preset.Buttons.FirstOrDefault(b => b.Id == buttonId);
            if (btn != null)
            {
                preset.Buttons.Remove(btn);
                SaveAll();
            }
        }

        private void SaveAll() => _storage.Save(Presets.ToList());
    }
}
