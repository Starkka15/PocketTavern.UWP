using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class CharactersPage : Page
    {
        private readonly CharactersViewModel _vm = new CharactersViewModel();

        public CharactersPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await _vm.LoadAsync();
            CharactersList.ItemsSource = _vm.Characters;
            UpdateEmptyState();
        }

        private void UpdateEmptyState()
        {
            bool empty = _vm.Characters.Count == 0;
            EmptyState.Visibility = empty ? Windows.UI.Xaml.Visibility.Visible : Windows.UI.Xaml.Visibility.Collapsed;
            CharactersList.Visibility = empty ? Windows.UI.Xaml.Visibility.Collapsed : Windows.UI.Xaml.Visibility.Visible;
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
            => App.Navigation.GoBack();

        private void OnCreateClick(object sender, RoutedEventArgs e)
            => App.Navigation.NavigateToCreateCharacter();

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await _vm.LoadAsync();
            CharactersList.ItemsSource = _vm.Characters;
            UpdateEmptyState();
        }

        private void OnSearchChanged(object sender, TextChangedEventArgs e)
            => _vm.SearchQuery = SearchBox.Text;

        private void OnCharacterClicked(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is CharacterListItem item)
                App.Navigation.NavigateToChat(item.Avatar ?? item.Name);
        }

        // Context menu — the Button's Click opens the flyout automatically via Button.Flyout
        private void OnCharacterMenuClick(object sender, RoutedEventArgs e) { }

        private void OnCharacterChatClick(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is CharacterListItem item)
                App.Navigation.NavigateToChat(item.Avatar ?? item.Name);
        }

        private void OnCharacterEditClick(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is CharacterListItem item)
                App.Navigation.NavigateToCharacterSettings(item.Avatar ?? item.Name);
        }

        private async void OnCharacterFavoriteClick(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is CharacterListItem item)
            {
                var ch = item.Character;
                ch.IsFavorite = !ch.IsFavorite;
                await App.Characters.SaveCharacterAsync(item.Avatar ?? item.Name, ch);
                await _vm.LoadAsync();
                CharactersList.ItemsSource = _vm.Characters;
                UpdateEmptyState();
            }
        }

        private async void OnCharacterDeleteClick(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is CharacterListItem item)
            {
                var dialog = new Windows.UI.Xaml.Controls.ContentDialog
                {
                    Title = "Delete Character",
                    Content = $"Delete \"{item.Name}\" and all their chats? This cannot be undone.",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel",
                    DefaultButton = Windows.UI.Xaml.Controls.ContentDialogButton.Close,
                    RequestedTheme = Windows.UI.Xaml.ElementTheme.Dark
                };
                var result = await dialog.ShowAsync();
                if (result == Windows.UI.Xaml.Controls.ContentDialogResult.Primary)
                {
                    await App.Characters.DeleteCharacterAsync(item.Avatar ?? item.Name);
                    await _vm.LoadAsync();
                    CharactersList.ItemsSource = _vm.Characters;
                    UpdateEmptyState();
                }
            }
        }
    }
}
