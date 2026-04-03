using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class SettingsPage : Page
    {
        private readonly SettingsViewModel _vm = new SettingsViewModel();

        public SettingsPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _vm.Load();
            ConnectionStatusLabel.Text = _vm.ConnectionStatusText;
        }

        private void OnBackClick(object sender, RoutedEventArgs e)      => App.Navigation.GoBack();
        private void OnApiConfigClick(object sender, RoutedEventArgs e) => App.Navigation.NavigateToApiConfig();
        private void OnTextGenClick(object sender, RoutedEventArgs e)            => App.Navigation.NavigateToTextGen();
        private void OnOaiPresetClick(object sender, RoutedEventArgs e)         => App.Navigation.NavigateToOaiPreset();
        private void OnFormattingClick(object sender, RoutedEventArgs e)         => App.Navigation.NavigateToFormatting();
        private void OnContextSettingsClick(object sender, RoutedEventArgs e)    => App.Navigation.NavigateToContextSettings();
        private void OnWorldInfoClick(object sender, RoutedEventArgs e)          => App.Navigation.NavigateToWorldInfo();
        private void OnPersonaClick(object sender, RoutedEventArgs e)            => App.Navigation.NavigateToPersona();
        private void OnAppearanceClick(object sender, RoutedEventArgs e)         => App.Navigation.NavigateToTheme();
        private void OnExtensionsClick(object sender, RoutedEventArgs e)         => App.Navigation.NavigateToExtensions();
        private void OnConnectionProfilesClick(object sender, RoutedEventArgs e) => App.Navigation.NavigateToConnectionProfiles();
        private void OnCardSearchClick(object sender, RoutedEventArgs e)         => App.Navigation.NavigateToCharaVault();
        private void OnImageGenClick(object sender, RoutedEventArgs e)           => App.Navigation.NavigateToImageGenSettings();
        private void OnTtsClick(object sender, RoutedEventArgs e)                => App.Navigation.NavigateToTtsSettings();
        private void OnDebugLogClick(object sender, RoutedEventArgs e)           => App.Navigation.NavigateToDebugLog();
        private void OnStImportClick(object sender, RoutedEventArgs e)           => App.Navigation.NavigateToStImport();
        private void OnHelpClick(object sender, RoutedEventArgs e)               => App.Navigation.NavigateToSetupGuide();
    }
}
