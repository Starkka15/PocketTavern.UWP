using System.Collections.ObjectModel;
using System.Threading.Tasks;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.ViewModels
{
    public class ConnectionProfilesViewModel : ViewModelBase
    {
        private ObservableCollection<ConnectionProfile> _profiles = new ObservableCollection<ConnectionProfile>();
        private ConnectionProfile _selected;

        public ObservableCollection<ConnectionProfile> Profiles
        {
            get => _profiles;
            set => Set(ref _profiles, value);
        }

        public ConnectionProfile Selected
        {
            get => _selected;
            set => Set(ref _selected, value);
        }

        public async Task LoadAsync()
        {
            var list = await App.ConnectionProfiles.GetAllAsync();
            Profiles.Clear();
            foreach (var p in list) Profiles.Add(p);
        }

        public async Task ActivateAsync(ConnectionProfile profile)
        {
            // Write profile's API settings to settings store
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
            App.Settings.SetLastActivatedProfileId(profile.Id);
        }

        public async Task DeleteAsync(ConnectionProfile profile)
        {
            await App.ConnectionProfiles.DeleteAsync(profile.Id);
            await LoadAsync();
        }

        public async Task SaveNewAsync(ConnectionProfile profile)
        {
            await App.ConnectionProfiles.UpsertAsync(profile);
            await LoadAsync();
        }
    }
}
