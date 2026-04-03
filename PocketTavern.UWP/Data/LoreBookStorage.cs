using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Newtonsoft.Json;
using PocketTavern.UWP.Models;
using PocketTavern.UWP.Services;

namespace PocketTavern.UWP.Data
{
    /// <summary>
    /// Stores world info / lorebooks as ST-compatible JSON files in LocalFolder\worlds\.
    /// Mirrors Android LoreBookStorage.
    /// </summary>
    public class LoreBookStorage
    {
        private readonly string _worldsDir;

        public LoreBookStorage()
        {
            _worldsDir = Path.Combine(ApplicationData.Current.LocalFolder.Path, "worlds");
            Directory.CreateDirectory(_worldsDir);
        }

        /// <summary>List all lorebook names (filenames without extension).</summary>
        public Task<List<string>> ListLorebooksAsync()
        {
            var names = Directory.GetFiles(_worldsDir, "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .OrderBy(n => n)
                .ToList();
            return Task.FromResult(names);
        }

        /// <summary>Load a lorebook and return its WorldInfoEntry list.</summary>
        public async Task<List<WorldInfoEntry>> LoadLorebookAsync(string name)
        {
            var path = Path.Combine(_worldsDir, name + ".json");
            if (!File.Exists(path)) return new List<WorldInfoEntry>();
            try
            {
                var text = await Task.Run(() => File.ReadAllText(path));
                var wif = JsonConvert.DeserializeObject<WorldInfoFile>(text);
                if (wif?.Entries == null) return new List<WorldInfoEntry>();

                return wif.Entries.Values.Select(e => new WorldInfoEntry
                {
                    Uid = e.Uid.ToString(),
                    Keys = e.Key ?? new List<string>(),
                    SecondaryKeys = e.KeySecondary ?? new List<string>(),
                    Content = e.Content ?? "",
                    Comment = e.Comment ?? "",
                    Constant = e.Constant,
                    Selective = e.Selective,
                    Order = e.Order,
                    Position = e.Position,
                    Depth = e.Depth,
                    Probability = e.Probability,
                    Enabled = e.Enabled,
                    Group = e.Group ?? "",
                    ScanDepth = e.ScanDepth,
                    CaseSensitive = e.CaseSensitive,
                    MatchWholeWords = e.MatchWholeWords
                }).ToList();
            }
            catch (Exception ex)
            {
                DebugLogger.LogError("LoreBookStorage", $"Failed to load {name}", ex);
                return new List<WorldInfoEntry>();
            }
        }

        /// <summary>Save a lorebook from a WorldInfoEntry list.</summary>
        public async Task SaveLorebookAsync(string name, List<WorldInfoEntry> entries)
        {
            var fileEntries = new Dictionary<string, WorldInfoFileEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                fileEntries[i.ToString()] = new WorldInfoFileEntry
                {
                    Uid = int.TryParse(e.Uid, out var uid) ? uid : i,
                    Key = e.Keys,
                    KeySecondary = e.SecondaryKeys,
                    Comment = e.Comment,
                    Content = e.Content,
                    Constant = e.Constant,
                    Selective = e.Selective,
                    Order = e.Order,
                    Position = e.Position,
                    Depth = e.Depth,
                    Probability = e.Probability,
                    Enabled = e.Enabled,
                    Group = e.Group,
                    ScanDepth = e.ScanDepth,
                    CaseSensitive = e.CaseSensitive,
                    MatchWholeWords = e.MatchWholeWords
                };
            }

            var wif = new WorldInfoFile { Name = name, Entries = fileEntries };
            var json = JsonConvert.SerializeObject(wif, Formatting.None);
            await Task.Run(() => File.WriteAllText(Path.Combine(_worldsDir, name + ".json"), json));
        }

        /// <summary>Delete a lorebook by name.</summary>
        public Task DeleteLorebookAsync(string name)
        {
            var path = Path.Combine(_worldsDir, name + ".json");
            if (File.Exists(path)) File.Delete(path);
            return Task.CompletedTask;
        }

        /// <summary>Save raw JSON bytes as a lorebook file (used by CharaVault importer).</summary>
        public Task SaveRawLorebookAsync(string name, byte[] bytes)
        {
            return Task.Run(() => File.WriteAllBytes(Path.Combine(_worldsDir, name + ".json"), bytes));
        }

        // ── ST-compatible JSON schema ─────────────────────────────────────────────

        private class WorldInfoFile
        {
            [JsonProperty("name")]
            public string Name { get; set; } = "";

            [JsonProperty("description")]
            public string Description { get; set; } = "";

            [JsonProperty("entries")]
            public Dictionary<string, WorldInfoFileEntry> Entries { get; set; } = new Dictionary<string, WorldInfoFileEntry>();
        }

        private class WorldInfoFileEntry
        {
            [JsonProperty("uid")]
            public int Uid { get; set; } = 0;

            [JsonProperty("key")]
            public List<string> Key { get; set; } = new List<string>();

            [JsonProperty("keysecondary")]
            public List<string> KeySecondary { get; set; } = new List<string>();

            [JsonProperty("comment")]
            public string Comment { get; set; } = "";

            [JsonProperty("content")]
            public string Content { get; set; } = "";

            [JsonProperty("constant")]
            public bool Constant { get; set; } = false;

            [JsonProperty("selective")]
            public bool Selective { get; set; } = false;

            [JsonProperty("order")]
            public int Order { get; set; } = 100;

            [JsonProperty("position")]
            public int Position { get; set; } = 0;

            [JsonProperty("depth")]
            public int Depth { get; set; } = 4;

            [JsonProperty("probability")]
            public int Probability { get; set; } = 100;

            [JsonProperty("enabled")]
            public bool Enabled { get; set; } = true;

            [JsonProperty("group")]
            public string Group { get; set; } = "";

            [JsonProperty("scan_depth")]
            public int? ScanDepth { get; set; }

            [JsonProperty("case_sensitive")]
            public bool CaseSensitive { get; set; } = false;

            [JsonProperty("match_whole_words")]
            public bool MatchWholeWords { get; set; } = false;
        }
    }
}
