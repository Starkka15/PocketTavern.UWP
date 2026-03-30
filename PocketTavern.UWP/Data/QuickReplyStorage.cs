using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using PocketTavern.UWP.Models;
using Windows.Storage;

namespace PocketTavern.UWP.Data
{
    /// <summary>
    /// Persists Quick Reply presets to LocalFolder/extensions/quick_reply.json.
    /// Mirrors Android's ExtensionStorage quick reply methods.
    /// </summary>
    public class QuickReplyStorage
    {
        private readonly string _filePath;

        public QuickReplyStorage()
        {
            var dir = Path.Combine(ApplicationData.Current.LocalFolder.Path, "extensions");
            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "quick_reply.json");
        }

        public List<QuickReplyPreset> Load()
        {
            if (!File.Exists(_filePath))
                return DefaultPresets();
            try
            {
                return JsonConvert.DeserializeObject<List<QuickReplyPreset>>(File.ReadAllText(_filePath))
                       ?? DefaultPresets();
            }
            catch { return DefaultPresets(); }
        }

        public void Save(List<QuickReplyPreset> presets)
        {
            File.WriteAllText(_filePath, JsonConvert.SerializeObject(presets, Formatting.Indented));
        }

        private static List<QuickReplyPreset> DefaultPresets() =>
            new List<QuickReplyPreset>
            {
                new QuickReplyPreset
                {
                    Name    = "Default",
                    Enabled = true,
                    Buttons = new List<QuickReplyButton>
                    {
                        new QuickReplyButton { Label = "Continue",  Message = "Continue." },
                        new QuickReplyButton { Label = "Go on",     Message = "Go on." },
                        new QuickReplyButton { Label = "Summarize", Message = "Please summarize what has happened so far." }
                    }
                }
            };
    }
}
