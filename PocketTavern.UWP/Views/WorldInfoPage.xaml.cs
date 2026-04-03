using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class WorldInfoPage : Page
    {
        private readonly WorldInfoViewModel _vm = new WorldInfoViewModel();
        public WorldInfoPage() { this.InitializeComponent(); }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _vm.Load();
            WorldsList.ItemsSource = _vm.Items;
            EmptyState.Visibility = _vm.Items.Count == 0
                ? Windows.UI.Xaml.Visibility.Visible
                : Windows.UI.Xaml.Visibility.Collapsed;
        }

        private void OnBackClick(object sender, RoutedEventArgs e) => App.Navigation.GoBack();

        private void OnLorebookClicked(object sender, ItemClickEventArgs e)
        {
            // Drill-down into lorebook entries (future: navigate to entries page)
        }

        private void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            _vm.Load();
            WorldsList.ItemsSource = null;
            WorldsList.ItemsSource = _vm.Items;
            EmptyState.Visibility = _vm.Items.Count == 0
                ? Windows.UI.Xaml.Visibility.Visible
                : Windows.UI.Xaml.Visibility.Collapsed;
        }
    }
}
