using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.Models;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class ConnectionProfilesPage : Page
    {
        private readonly ConnectionProfilesViewModel _vm = new ConnectionProfilesViewModel();
        public ConnectionProfilesPage() { this.InitializeComponent(); }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await _vm.LoadAsync();
            ProfilesList.ItemsSource = _vm.Profiles;
        }

        private void OnBackClick(object sender, RoutedEventArgs e) => App.Navigation.GoBack();
        private void OnAddClick(object sender, RoutedEventArgs e) { /* reserved for add-profile dialog */ }
        private void OnProfileClicked(object sender, ItemClickEventArgs e) { }
        private async void OnActivateClick(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is ConnectionProfile profile)
            {
                await _vm.ActivateAsync(profile);
                ActiveLabel.Text = $"Active: {profile.Name}";
            }
        }
    }
}
