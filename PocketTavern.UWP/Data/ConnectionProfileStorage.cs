using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Newtonsoft.Json;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.Data
{
    /// <summary>
    /// Stores connection profiles as a single JSON file in LocalFolder.
    /// </summary>
    public class ConnectionProfileStorage
    {
        private readonly string _filePath;

        public ConnectionProfileStorage()
        {
            _filePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "connection_profiles.json");
        }

        public async Task<List<ConnectionProfile>> GetAllAsync()
        {
            if (!File.Exists(_filePath)) return new List<ConnectionProfile>();
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(_filePath);
                var json = await FileIO.ReadTextAsync(file);
                return JsonConvert.DeserializeObject<List<ConnectionProfile>>(json)
                    ?? new List<ConnectionProfile>();
            }
            catch { return new List<ConnectionProfile>(); }
        }

        public async Task SaveAllAsync(List<ConnectionProfile> profiles)
        {
            var json = JsonConvert.SerializeObject(profiles, Formatting.Indented);
            var folder = await StorageFolder.GetFolderFromPathAsync(
                Path.GetDirectoryName(_filePath));
            var file = await folder.CreateFileAsync(
                Path.GetFileName(_filePath), CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, json);
        }

        public async Task UpsertAsync(ConnectionProfile profile)
        {
            var profiles = await GetAllAsync();
            var idx = profiles.FindIndex(p => p.Id == profile.Id);
            if (idx >= 0) profiles[idx] = profile;
            else profiles.Add(profile);
            await SaveAllAsync(profiles);
        }

        public async Task DeleteAsync(string profileId)
        {
            var profiles = await GetAllAsync();
            profiles.RemoveAll(p => p.Id == profileId);
            await SaveAllAsync(profiles);
        }
    }
}
