using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Security.Credentials;
using Windows.Storage;
using Newtonsoft.Json;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.Data
{
    /// <summary>
    /// Stores connection profiles as a single JSON file in LocalFolder.
    /// API keys are stored separately in PasswordVault (never written to JSON).
    /// </summary>
    public class ConnectionProfileStorage
    {
        private const string VaultResource = "PocketTavernApiKey";
        private readonly string _filePath;
        private readonly PasswordVault _vault = new PasswordVault();

        public ConnectionProfileStorage()
        {
            _filePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "connection_profiles.json");
        }

        public async Task<List<ConnectionProfile>> GetAllAsync()
        {
            if (!File.Exists(_filePath)) return new List<ConnectionProfile>();
            List<ConnectionProfile> profiles;
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(_filePath);
                var json = await FileIO.ReadTextAsync(file);
                profiles = JsonConvert.DeserializeObject<List<ConnectionProfile>>(json)
                    ?? new List<ConnectionProfile>();
            }
            catch { return new List<ConnectionProfile>(); }

            bool needsMigration = false;
            foreach (var profile in profiles)
            {
                if (!string.IsNullOrEmpty(profile.ApiKey))
                {
                    // Plaintext key still in JSON — migrate to PasswordVault
                    try
                    {
                        try { _vault.Remove(_vault.Retrieve(VaultResource, profile.Id)); } catch { }
                        _vault.Add(new PasswordCredential(VaultResource, profile.Id, profile.ApiKey));
                    }
                    catch { }
                    needsMigration = true;
                }
                else
                {
                    try
                    {
                        var cred = _vault.Retrieve(VaultResource, profile.Id);
                        cred.RetrievePassword();
                        profile.ApiKey = cred.Password;
                    }
                    catch { profile.ApiKey = ""; }
                }
            }

            if (needsMigration)
                await SaveAllAsync(profiles);

            return profiles;
        }

        public async Task SaveAllAsync(List<ConnectionProfile> profiles)
        {
            foreach (var profile in profiles)
            {
                try
                {
                    try { _vault.Remove(_vault.Retrieve(VaultResource, profile.Id)); } catch { }
                    if (!string.IsNullOrEmpty(profile.ApiKey))
                        _vault.Add(new PasswordCredential(VaultResource, profile.Id, profile.ApiKey));
                }
                catch { }
            }

            // ShouldSerializeApiKey() returns false — ApiKey never written to JSON
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
            try { _vault.Remove(_vault.Retrieve(VaultResource, profileId)); } catch { }
            await SaveAllAsync(profiles);
        }
    }
}
