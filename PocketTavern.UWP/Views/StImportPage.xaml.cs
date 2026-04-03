using System.Collections.Specialized;
using PocketTavern.UWP.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace PocketTavern.UWP.Views
{
    public sealed partial class StImportPage : Page
    {
        private readonly StImportViewModel _vm = new StImportViewModel();

        public StImportPage()
        {
            InitializeComponent();
            LogList.ItemsSource = _vm.Log;
            _vm.Log.CollectionChanged += OnLogChanged;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _vm.ResetState();
        }

        private void OnLogChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_vm.Log.Count > 0)
                LogList.ScrollIntoView(_vm.Log[_vm.Log.Count - 1]);
        }

        private async void FolderImportButton_Click(object sender, RoutedEventArgs e)
        {
            SetImportingState(true);
            await _vm.ImportFromFolderAsync();
            SetImportingState(false);
            ShowResults();
        }

        private async void ServerImportButton_Click(object sender, RoutedEventArgs e)
        {
            SetImportingState(true);
            await _vm.ImportFromServerAsync();
            SetImportingState(false);
            ShowResults();
        }

        private void SetImportingState(bool importing)
        {
            FolderImportButton.IsEnabled = !importing;
            ServerImportButton.IsEnabled = !importing;
            ProgressPanel.Visibility = importing ? Visibility.Visible : Visibility.Collapsed;
            LogPanel.Visibility = Visibility.Visible;
            ResultsPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowResults()
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
            if (_vm.IsComplete)
            {
                ResultsPanel.Visibility = Visibility.Visible;
                ResultsText.Text = $"Characters: {_vm.CharactersImported}, " +
                    $"Lorebooks: {_vm.LorebooksImported}, " +
                    $"Chats: {_vm.ChatsImported}, " +
                    $"Errors: {_vm.Errors}";
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            _vm.ResetState();
            LogPanel.Visibility = Visibility.Collapsed;
            ResultsPanel.Visibility = Visibility.Collapsed;
            FolderImportButton.IsEnabled = true;
            ServerImportButton.IsEnabled = true;
        }

        private void ServerUrlBox_TextChanged(object sender, TextChangedEventArgs e)
            => _vm.ServerUrl = ServerUrlBox.Text;

        private void UsernameBox_TextChanged(object sender, TextChangedEventArgs e)
            => _vm.Username = UsernameBox.Text;

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
            => _vm.Password = PasswordBox.Password;

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }
}
