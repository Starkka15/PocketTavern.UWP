using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.Services;

namespace PocketTavern.UWP.Views
{
    /// <summary>
    /// Hosts realm.risuai.net in a WebView.
    /// Intercepts .charx download navigations and imports them locally. (V24)
    /// </summary>
    public sealed partial class RisuRealmPage : Page
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        private static readonly Uri _realmUri = new Uri("https://realm.risuai.net/");

        public RisuRealmPage() { this.InitializeComponent(); }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            RealmWebView.Navigate(_realmUri);
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
            => App.Navigation.GoBack();

        private void OnRefreshClick(object sender, RoutedEventArgs e)
            => RealmWebView.Navigate(_realmUri);

        private async void OnNavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            // Intercept .charx navigation (fires for direct link clicks before response)
            if (IsCharxUri(args.Uri))
            {
                args.Cancel = true;
                await ImportFromUriAsync(args.Uri);
            }
        }

        private async void OnUnviewableContent(WebView sender, WebViewUnviewableContentIdentifiedEventArgs args)
        {
            // Fires when the server returns unrecognised MIME or Content-Disposition:attachment.
            // RisuRealm download URLs are /api/download/{id} — no .charx in the URL — so intercept
            // anything unviewable from this WebView (it only hosts risuai.net anyway).
            await ImportFromUriAsync(args.Uri);
        }

        private async void OnNewWindowRequested(WebView sender, WebViewNewWindowRequestedEventArgs args)
        {
            // Intercept any new-window request that looks like a download or explicit .charx link
            if (IsCharxUri(args.Uri) || args.Uri?.Host?.Contains("risuai.net") == true)
            {
                args.Handled = true;
                await ImportFromUriAsync(args.Uri);
            }
        }

        private static bool IsCharxUri(Uri uri)
        {
            if (uri == null) return false;
            var lower = uri.AbsoluteUri.ToLowerInvariant();
            return lower.EndsWith(".charx") || lower.Contains(".charx?") || lower.Contains(".charx&");
        }

        private async System.Threading.Tasks.Task ImportFromUriAsync(Uri uri)
        {
            ShowStatus("Downloading card...");
            try
            {
                var bytes = await _http.GetByteArrayAsync(uri);
                var fileName = System.IO.Path.GetFileNameWithoutExtension(
                    uri.Segments[uri.Segments.Length - 1]);
                if (string.IsNullOrWhiteSpace(fileName)) fileName = "imported_card";

                var tempFile = await CreateTempStorageFileAsync(bytes, fileName + ".charx");
                if (tempFile != null)
                {
                    await App.Characters.ImportCharacterCardAsync(tempFile);
                    ShowStatus($"Imported: {fileName}");
                }
                else
                {
                    ShowStatus("Import failed: could not create temp file");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Import failed: {ex.Message}");
            }
        }

        private void ShowStatus(string message)
        {
            ImportStatusText.Text = message;
            ImportStatusBar.Visibility = Visibility.Visible;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            timer.Tick += (s, e) =>
            {
                ImportStatusBar.Visibility = Visibility.Collapsed;
                ((DispatcherTimer)s).Stop();
            };
            timer.Start();
        }

        private static async Task<Windows.Storage.StorageFile> CreateTempStorageFileAsync(byte[] bytes, string name)
        {
            try
            {
                var tempFolder = Windows.Storage.ApplicationData.Current.TemporaryFolder;
                var safe = string.Concat(name.Select(c =>
                    System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
                var file = await tempFolder.CreateFileAsync(safe,
                    Windows.Storage.CreationCollisionOption.ReplaceExisting);
                await Windows.Storage.FileIO.WriteBytesAsync(file, bytes);
                return file;
            }
            catch { return null; }
        }
    }
}
