using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.UI.Xaml;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private ObservableCollection<Character> _recentCharacters = new ObservableCollection<Character>();
        private string _connectionStatus = "Disconnected";
        private bool _isConnected = false;
        private string _apiDisplayName = "";

        public ObservableCollection<Character> RecentCharacters
        {
            get => _recentCharacters;
            set => Set(ref _recentCharacters, value);
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => Set(ref _connectionStatus, value);
        }

        public bool IsConnected
        {
            get => _isConnected;
            set => Set(ref _isConnected, value);
        }

        public string ApiDisplayName
        {
            get => _apiDisplayName;
            set => Set(ref _apiDisplayName, value);
        }

        public async Task LoadAsync()
        {
            var config = App.Settings.GetLlmConfig();
            ApiDisplayName = config.DisplayName;

            var characters = await App.Characters.GetAllCharactersAsync();
            RecentCharacters.Clear();
            // Show up to 6 most recently chatted characters
            foreach (var ch in characters)
                RecentCharacters.Add(ch);
        }

        public void NavigateToCharacters()
            => App.Navigation.NavigateToCharacters();

        public void NavigateToRecentChats()
            => App.Navigation.NavigateToRecentChats();

        public void NavigateToSettings()
            => App.Navigation.NavigateToSettings();
    }
}
