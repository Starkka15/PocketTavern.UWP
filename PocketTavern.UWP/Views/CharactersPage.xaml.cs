using System.Collections.Generic;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class CharactersPage : Page
    {
        private readonly CharactersViewModel _vm = new CharactersViewModel();
        private bool _showGroups = false;

        public CharactersPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await _vm.LoadAsync();
            CharactersList.ItemsSource = _vm.Characters;
            UpdateTabVisibility();
        }

        private void SetActiveTab(bool groups)
        {
            _showGroups = groups;
            TabTitleText.Text = groups ? "Groups" : "Characters";
            UpdateTabVisibility();
        }

        private void UpdateTabVisibility()
        {
            if (_showGroups)
            {
                SearchBox.Visibility = Visibility.Collapsed;
                CharactersList.Visibility = Visibility.Collapsed;
                EmptyState.Visibility = Visibility.Collapsed;
                GroupsPanel.Visibility = Visibility.Visible;
            }
            else
            {
                SearchBox.Visibility = Visibility.Visible;
                GroupsPanel.Visibility = Visibility.Collapsed;
                bool empty = _vm.Characters.Count == 0;
                EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
                CharactersList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void OnTabCharactersClick(object sender, RoutedEventArgs e)
            => SetActiveTab(false);

        private void OnTabGroupsClick(object sender, RoutedEventArgs e)
            => App.Navigation.NavigateToGroups();

        private void OnBackClick(object sender, RoutedEventArgs e)
            => App.Navigation.GoBack();

        private void OnCreateClick(object sender, RoutedEventArgs e)
        {
            if (_showGroups)
                App.Navigation.NavigateToGroups();
            else
                App.Navigation.NavigateToCreateCharacter();
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            if (!_showGroups)
            {
                await _vm.LoadAsync();
                CharactersList.ItemsSource = _vm.Characters;
                UpdateTabVisibility();
            }
        }

        private async void OnImportClick(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.Downloads
            };
            picker.FileTypeFilter.Add("*");

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            await _vm.ImportLocalCharacterAsync(file);
            CharactersList.ItemsSource = _vm.Characters;
            UpdateTabVisibility();

            if (_vm.ShowTranslateDialog)
                await ShowTranslateDialogAsync();

            if (!string.IsNullOrEmpty(_vm.TranslateError))
            {
                var err = new ContentDialog
                {
                    Title = "Import Error",
                    Content = _vm.TranslateError,
                    CloseButtonText = "OK",
                    RequestedTheme = Windows.UI.Xaml.ElementTheme.Dark
                };
                await err.ShowAsync();
            }
        }

        private async System.Threading.Tasks.Task ShowTranslateDialogAsync()
        {
            var result = await TranslateDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var fields = new List<string>();
                if (ChkDescription.IsChecked == true)        fields.Add("description");
                if (ChkPersonality.IsChecked == true)        fields.Add("personality");
                if (ChkScenario.IsChecked == true)           fields.Add("scenario");
                if (ChkFirstMessage.IsChecked == true)       fields.Add("first_mes");
                if (ChkAlternateGreetings.IsChecked == true) fields.Add("alternate_greetings");
                if (ChkMesExample.IsChecked == true)         fields.Add("mes_example");
                if (ChkSystemPrompt.IsChecked == true)       fields.Add("system_prompt");
                if (ChkCreatorNotes.IsChecked == true)       fields.Add("creator_notes");

                TranslatingOverlay.Visibility = Visibility.Visible;
                await _vm.TranslateAsync(fields);
                TranslatingOverlay.Visibility = Visibility.Collapsed;

                CharactersList.ItemsSource = _vm.Characters;
                UpdateTabVisibility();

                if (!string.IsNullOrEmpty(_vm.TranslateError))
                {
                    var err = new ContentDialog
                    {
                        Title = "Translation Error",
                        Content = _vm.TranslateError,
                        CloseButtonText = "OK",
                        RequestedTheme = Windows.UI.Xaml.ElementTheme.Dark
                    };
                    await err.ShowAsync();
                }
            }
            else
            {
                _vm.SkipTranslation();
            }
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
                UpdateTabVisibility();
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
                    UpdateTabVisibility();
                }
            }
        }
    }
}
