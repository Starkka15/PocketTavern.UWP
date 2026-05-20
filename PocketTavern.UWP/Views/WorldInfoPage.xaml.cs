using PocketTavern.UWP.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace PocketTavern.UWP.Views
{
    public sealed partial class WorldInfoPage : Page
    {
        private readonly WorldInfoViewModel _vm = new WorldInfoViewModel();
        public WorldInfoPage() { this.InitializeComponent(); }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            Reload();
        }

        private void Reload()
        {
            _vm.Load();
            WorldsList.ItemsSource = null;
            WorldsList.ItemsSource = _vm.Items;
            EmptyState.Visibility = _vm.Items.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnBackClick(object sender, RoutedEventArgs e) => App.Navigation.GoBack();

        private void OnRefreshClick(object sender, RoutedEventArgs e) => Reload();

        private void OnOpenLorebookClick(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button)?.Tag as WorldInfoItem;
            if (item == null) return;
            App.Navigation.NavigateToLorebookEntries(item.Name);
        }

        private void OnLorebookClicked(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as WorldInfoItem;
            if (item == null) return;
            App.Navigation.NavigateToLorebookEntries(item.Name);
        }

        private async void OnNewLorebookClick(object sender, RoutedEventArgs e)
        {
            var nameBox = new TextBox
            {
                PlaceholderText = "Lorebook name",
                Header = "Name"
            };
            var dialog = new ContentDialog
            {
                Title = "New Lorebook",
                Content = nameBox,
                PrimaryButtonText = "Create",
                CloseButtonText = "Cancel"
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            var name = nameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name)) return;
            await _vm.CreateLorebookAsync(name);
            Reload();
            App.Navigation.NavigateToLorebookEntries(name);
        }

        private async void OnDeleteLorebookClick(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button)?.Tag as WorldInfoItem;
            if (item == null) return;
            var dialog = new ContentDialog
            {
                Title = "Delete lorebook?",
                Content = $"Delete \"{item.Name}\"? This cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel"
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            await _vm.DeleteLorebookAsync(item.Name);
            Reload();
        }
    }
}
