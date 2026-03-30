using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.Data;
using PocketTavern.UWP.Services;

namespace PocketTavern.UWP
{
    sealed partial class App : Application
    {
        public static SettingsStorage Settings { get; private set; }
        public static CharacterStorage Characters { get; private set; }
        public static ChatStorage Chats { get; private set; }
        public static ConnectionProfileStorage ConnectionProfiles { get; private set; }
        public static PresetStorage Presets { get; private set; }
        public static NavigationService Navigation { get; private set; }
        public static ThemeManager Theme { get; private set; }
        public static JsExtensionHost Extensions { get; private set; }

        private static Exception _startupException;

        public App()
        {
            this.UnhandledException += OnUnhandledException;
            try
            {
                this.InitializeComponent();
                this.Suspending += OnSuspending;

                Settings = new SettingsStorage();
                Characters = new CharacterStorage();
                Chats = new ChatStorage();
                ConnectionProfiles = new ConnectionProfileStorage();
                Presets = new PresetStorage();
                Navigation = new NavigationService();
                Theme = new ThemeManager();
                Extensions = new JsExtensionHost();
            }
            catch (Exception ex)
            {
                _startupException = ex;
            }
        }

        private async void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            var dialog = new Windows.UI.Popups.MessageDialog(e.Exception?.ToString() ?? e.Message, "Unhandled Error");
            await dialog.ShowAsync();
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            if (_startupException != null)
            {
                Window.Current.Content = new Frame();
                Window.Current.Activate();
                var dialog = new Windows.UI.Popups.MessageDialog(_startupException.ToString(), "Startup Error");
                await dialog.ShowAsync();
                return;
            }

            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;

                // Initialize database
                DatabaseHelper.Initialize();

                // Copy bundled presets to local storage on first launch
                await Presets.SeedBundledPresetsAsync();

                // Load and apply selected theme before showing any UI
                await Theme.InitializeAsync(Settings.GetThemeKey());

                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                    rootFrame.Navigate(typeof(Views.MainPage), e.Arguments);
                Window.Current.Activate();
            }

            Navigation.SetFrame(rootFrame);
        }

        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}
