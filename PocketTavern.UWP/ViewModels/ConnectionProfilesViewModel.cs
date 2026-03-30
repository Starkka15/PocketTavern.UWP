using System.Collections.ObjectModel;
using System.Threading.Tasks;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.ViewModels
{
    public class ConnectionProfilesViewModel : ViewModelBase
    {
        private ObservableCollection<ConnectionProfile> _profiles = new ObservableCollection<ConnectionProfile>();
        private string _activeProfileId;

        public ObservableCollection<ConnectionProfile> Profiles
        {
            get => _profiles;
            set => Set(ref _profiles, value);
        }

        public string ActiveProfileId
        {
            get => _activeProfileId;
            set => Set(ref _activeProfileId, value);
        }

        public async Task LoadAsync()
        {
            var list = await App.ConnectionProfiles.GetAllAsync();
            Profiles.Clear();
            foreach (var p in list) Profiles.Add(p);
            ActiveProfileId = App.Settings.GetLastActivatedProfileId();
        }

        public ConnectionProfile GetCurrentAsProfile(string name)
        {
            var cfg = App.Settings.GetLlmConfig();
            return new ConnectionProfile
            {
                Name = name,
                MainApi = cfg.MainApi,
                TextGenType = cfg.TextGenType,
                ApiServer = cfg.ApiServer,
                ChatCompletionSource = cfg.ChatCompletionSource,
                CustomUrl = cfg.CustomUrl,
                ApiKey = cfg.ApiKey,
                Model = cfg.CurrentModel,
                TextGenPreset = App.Settings.GetSelectedTextGenPreset() ?? "",
                InstructPreset = App.Settings.GetSelectedInstructPreset() ?? "",
                ContextPreset = App.Settings.GetSelectedContextPreset() ?? "",
                SyspromptPreset = App.Settings.GetSelectedSyspromptPreset() ?? ""
            };
        }

        public async Task ActivateAsync(ConnectionProfile profile)
        {
            App.Settings.SaveLlmConfig(new ApiConfiguration
            {
                MainApi = profile.MainApi,
                TextGenType = profile.TextGenType,
                ApiServer = profile.ApiServer,
                ChatCompletionSource = profile.ChatCompletionSource,
                CustomUrl = profile.CustomUrl,
                ApiKey = profile.ApiKey,
                CurrentModel = profile.Model
            });
            if (!string.IsNullOrEmpty(profile.TextGenPreset))
                App.Settings.SetSelectedTextGenPreset(profile.TextGenPreset);
            if (!string.IsNullOrEmpty(profile.InstructPreset))
                App.Settings.SetSelectedInstructPreset(profile.InstructPreset);
            if (!string.IsNullOrEmpty(profile.ContextPreset))
                App.Settings.SetSelectedContextPreset(profile.ContextPreset);
            if (!string.IsNullOrEmpty(profile.SyspromptPreset))
                App.Settings.SetSelectedSyspromptPreset(profile.SyspromptPreset);
            App.Settings.SetLastActivatedProfileId(profile.Id);
            ActiveProfileId = profile.Id;
        }

        public async Task DeleteAsync(ConnectionProfile profile)
        {
            await App.ConnectionProfiles.DeleteAsync(profile.Id);
            if (ActiveProfileId == profile.Id)
            {
                ActiveProfileId = null;
                App.Settings.SetLastActivatedProfileId(null);
            }
            await LoadAsync();
        }

        public async Task SaveCurrentAsync(string name)
        {
            var profile = GetCurrentAsProfile(name);
            await App.ConnectionProfiles.UpsertAsync(profile);
            await LoadAsync();
            ActiveProfileId = profile.Id;
            App.Settings.SetLastActivatedProfileId(profile.Id);
        }
    }
}
