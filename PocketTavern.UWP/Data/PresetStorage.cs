using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using Newtonsoft.Json;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.Data
{
    /// <summary>
    /// Stores OAI presets, TextGen presets, instruct/context/sysprompt templates as JSON files.
    /// Layout mirrors SillyTavern's preset folder structure.
    /// </summary>
    public class PresetStorage
    {
        private readonly string _oaiPresetsDir;
        private readonly string _textgenPresetsDir;
        private readonly string _instructDir;
        private readonly string _contextDir;
        private readonly string _syspromptDir;

        public PresetStorage()
        {
            var local = ApplicationData.Current.LocalFolder.Path;
            _oaiPresetsDir   = Path.Combine(local, "presets", "oai");
            _textgenPresetsDir = Path.Combine(local, "presets", "textgen");
            _instructDir     = Path.Combine(local, "presets", "instruct");
            _contextDir      = Path.Combine(local, "presets", "context");
            _syspromptDir    = Path.Combine(local, "presets", "sysprompt");
            foreach (var dir in new[] { _oaiPresetsDir, _textgenPresetsDir, _instructDir, _contextDir, _syspromptDir })
                Directory.CreateDirectory(dir);
        }

        // ── OAI Presets ──────────────────────────────────────────────────────────

        public async Task<List<OaiPreset>> GetAllOaiPresetsAsync()
            => await LoadAllAsync<OaiPreset>(_oaiPresetsDir);

        public async Task<OaiPreset> GetOaiPresetAsync(string name)
            => await LoadAsync<OaiPreset>(_oaiPresetsDir, name);

        public async Task SaveOaiPresetAsync(OaiPreset preset)
            => await SaveAsync(_oaiPresetsDir, preset.Name, preset);

        public async Task DeleteOaiPresetAsync(string name)
            => await DeleteAsync(_oaiPresetsDir, name);

        // ── TextGen Presets ──────────────────────────────────────────────────────

        public async Task<List<TextGenPreset>> GetAllTextGenPresetsAsync()
            => await LoadAllAsync<TextGenPreset>(_textgenPresetsDir);

        public async Task<TextGenPreset> GetTextGenPresetAsync(string name)
            => await LoadAsync<TextGenPreset>(_textgenPresetsDir, name);

        public async Task SaveTextGenPresetAsync(TextGenPreset preset)
            => await SaveAsync(_textgenPresetsDir, preset.Name, preset);

        // ── Instruct Templates ───────────────────────────────────────────────────

        public async Task<List<InstructTemplate>> GetAllInstructTemplatesAsync()
            => await LoadAllAsync<InstructTemplate>(_instructDir);

        public async Task<InstructTemplate> GetInstructTemplateAsync(string name)
            => await LoadAsync<InstructTemplate>(_instructDir, name);

        public async Task SaveInstructTemplateAsync(InstructTemplate t)
            => await SaveAsync(_instructDir, t.Name, t);

        // ── Context Templates ────────────────────────────────────────────────────

        public async Task<List<ContextTemplate>> GetAllContextTemplatesAsync()
            => await LoadAllAsync<ContextTemplate>(_contextDir);

        public async Task<ContextTemplate> GetContextTemplateAsync(string name)
            => await LoadAsync<ContextTemplate>(_contextDir, name);

        // ── System Prompt Presets ────────────────────────────────────────────────

        public async Task<List<SystemPromptPreset>> GetAllSyspromptPresetsAsync()
            => await LoadAllAsync<SystemPromptPreset>(_syspromptDir);

        public async Task<SystemPromptPreset> GetSyspromptPresetAsync(string name)
            => await LoadAsync<SystemPromptPreset>(_syspromptDir, name);

        // ── Seeder: copy bundled Assets\Presets to local storage on first launch ──

        public async Task SeedBundledPresetsAsync()
        {
            string[] assetPaths = { @"Assets\Presets\instruct", @"Assets\Presets\context", @"Assets\Presets\textgen", @"Assets\Presets\oai", @"Assets\Presets\sysprompt" };
            string[] localDirs  = { _instructDir, _contextDir, _textgenPresetsDir, _oaiPresetsDir, _syspromptDir };

            for (int i = 0; i < assetPaths.Length; i++)
            {
                string assetRelPath = assetPaths[i];
                string localDir = localDirs[i];
                StorageFolder assetFolder;
                try
                {
                    assetFolder = await Package.Current.InstalledLocation.GetFolderAsync(assetRelPath);
                }
                catch { continue; }

                var localFolder = await StorageFolder.GetFolderFromPathAsync(localDir);
                var files = await assetFolder.GetFilesAsync();

                foreach (var file in files)
                {
                    var destPath = Path.Combine(localDir, file.Name);
                    if (!File.Exists(destPath))
                        await file.CopyAsync(localFolder, file.Name, NameCollisionOption.FailIfExists);
                }
            }
        }

        // ── Generic helpers ──────────────────────────────────────────────────────

        private async Task<List<T>> LoadAllAsync<T>(string dir)
        {
            var result = new List<T>();
            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                try
                {
                    var storageFile = await StorageFile.GetFileFromPathAsync(file);
                    var json = await FileIO.ReadTextAsync(storageFile);
                    var obj = JsonConvert.DeserializeObject<T>(json);
                    if (obj == null) continue;

                    // If the JSON had no "name" field, derive it from the filename
                    var nameProp = typeof(T).GetProperty("Name");
                    if (nameProp != null && string.IsNullOrEmpty(nameProp.GetValue(obj) as string))
                        nameProp.SetValue(obj, Path.GetFileNameWithoutExtension(file));

                    result.Add(obj);
                }
                catch { }
            }
            return result;
        }

        private async Task<T> LoadAsync<T>(string dir, string name) where T : class
        {
            var path = Path.Combine(dir, name + ".json");
            if (!File.Exists(path)) return null;
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(path);
                var json = await FileIO.ReadTextAsync(file);
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch { return null; }
        }

        private async Task SaveAsync<T>(string dir, string name, T obj)
        {
            var json = JsonConvert.SerializeObject(obj, Formatting.Indented);
            var folder = await StorageFolder.GetFolderFromPathAsync(dir);
            var file = await folder.CreateFileAsync(name + ".json", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, json);
        }

        private Task DeleteAsync(string dir, string name)
        {
            var path = Path.Combine(dir, name + ".json");
            if (File.Exists(path)) File.Delete(path);
            return Task.CompletedTask;
        }
    }
}
