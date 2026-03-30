using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class CharaVaultPage : Page
    {
        private readonly CharaVaultViewModel _vm = new CharaVaultViewModel();

        public CharaVaultPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _vm.Load();
            _vm.PropertyChanged += OnVmPropertyChanged;
            StatusLabel.Text  = _vm.StatusText;
            FooterStatus.Text = _vm.StatusText;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }

        private void OnVmPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CharaVaultViewModel.IsLoading))
            {
                LoadingRing.IsActive = _vm.IsLoading;
                if (_vm.IsLoading)
                {
                    EmptyState.Visibility = Visibility.Collapsed;
                }
            }
            if (e.PropertyName == nameof(CharaVaultViewModel.StatusText))
            {
                StatusLabel.Text = _vm.StatusText;
                FooterStatus.Text = _vm.StatusText;
            }
            if (e.PropertyName == nameof(CharaVaultViewModel.Results))
            {
                UpdateResultsView();
            }
            if (e.PropertyName == nameof(CharaVaultViewModel.HasMore))
            {
                LoadMoreButton.Visibility = _vm.HasMore ? Visibility.Visible : Visibility.Collapsed;
                FooterStatus.Visibility = _vm.HasMore ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void UpdateResultsView()
        {
            bool hasItems = _vm.Results.Count > 0;
            ResultsList.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
            EmptyState.Visibility = (hasItems || _vm.IsLoading) ? Visibility.Collapsed : Visibility.Visible;
            if (hasItems && ResultsList.ItemsSource == null)
                ResultsList.ItemsSource = _vm.Results;
        }

        private async void OnSearchClick(object sender, RoutedEventArgs e)
        {
            _vm.SearchQuery = SearchBox.Text;
            ResultsList.ItemsSource = null;
            await _vm.SearchAsync();
            ResultsList.ItemsSource = _vm.Results;
            UpdateResultsView();
        }

        private async void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                _vm.SearchQuery = SearchBox.Text;
                ResultsList.ItemsSource = null;
                await _vm.SearchAsync();
                ResultsList.ItemsSource = _vm.Results;
                UpdateResultsView();
            }
        }

        private void OnCardClicked(object sender, ItemClickEventArgs e) { /* detail view reserved */ }

        private async void OnImportClick(object sender, RoutedEventArgs e)
        {
            if (!((sender as Button)?.Tag is CharaVaultCardItem item)) return;
            var error = await _vm.ImportCharacterAsync(item);
            if (error != null)
            {
                var dialog = new ContentDialog
                {
                    Title = "Import Failed",
                    Content = error,
                    CloseButtonText = "OK",
                    RequestedTheme = Windows.UI.Xaml.ElementTheme.Dark
                };
                await dialog.ShowAsync();
            }
            else
            {
                StatusLabel.Text = $"Imported {item.Name}";
            }
        }

        private async void OnLoadMoreClick(object sender, RoutedEventArgs e)
            => await _vm.LoadMoreAsync();

        private void OnBackClick(object sender, RoutedEventArgs e)
            => App.Navigation.GoBack();

        private async void OnConfigureClick(object sender, RoutedEventArgs e)
        {
            var currentUrl = App.Settings.GetCharaVaultUrl();
            var box = new TextBox
            {
                Text = currentUrl,
                PlaceholderText = "https://charavault.net  (leave blank for default)",
                Style = (Windows.UI.Xaml.Style)Application.Current.Resources["DarkTextBoxStyle"]
            };
            var dialog = new ContentDialog
            {
                Title = "Card Search Server",
                Content = box,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                RequestedTheme = ElementTheme.Dark
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                App.Settings.SaveCharaVaultUrl(box.Text.Trim());
                _vm.Load();
                StatusLabel.Text = _vm.StatusText;
            }
        }
    }
}
