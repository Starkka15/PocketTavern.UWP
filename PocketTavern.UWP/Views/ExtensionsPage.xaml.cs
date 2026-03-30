using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class ExtensionsPage : Page
    {
        private readonly ExtensionsViewModel _vm = new ExtensionsViewModel();
        public ExtensionsPage() { this.InitializeComponent(); }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _vm.Load();
            ExtList.ItemsSource = _vm.Extensions;
        }

        private void OnBackClick(object sender, RoutedEventArgs e) => App.Navigation.GoBack();

        private void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            _vm.Load();
            ExtList.ItemsSource = null;
            ExtList.ItemsSource = _vm.Extensions;
        }
    }
}
