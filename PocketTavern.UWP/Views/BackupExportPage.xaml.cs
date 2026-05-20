using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class BackupExportPage : Page
    {
        private readonly BackupExportViewModel _vm = new BackupExportViewModel();

        public BackupExportPage() { InitializeComponent(); }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _vm.PropertyChanged += OnVmPropertyChanged;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }

        private void OnVmPropertyChanged(object sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_vm.IsExporting))
            {
                ExportButton.IsEnabled = !_vm.IsExporting;
                ProgressPanel.Visibility = _vm.IsExporting
                    ? Visibility.Visible : Visibility.Collapsed;
                if (!_vm.IsExporting && _vm.IsComplete)
                {
                    ResultsPanel.Visibility = Visibility.Visible;
                    ResultsText.Text = _vm.Status;
                }
            }
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            ResultsPanel.Visibility = Visibility.Collapsed;
            await _vm.ExportAsync();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }
}
