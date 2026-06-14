using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace PocketTavern.UWP.ViewModels
{
    public class CharaVaultCardItem : INotifyPropertyChanged
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public string Tagline { get; set; }
        public string AvatarUrl { get; set; }
        public int Stars { get; set; }
        public string FullPath { get; set; }
        public string Initial => Name?.Length > 0 ? Name[0].ToString().ToUpper() : "?";

        public event PropertyChangedEventHandler PropertyChanged;

        private ImageSource _avatarImage;
        public ImageSource AvatarImage
        {
            get => _avatarImage;
            set
            {
                if (_avatarImage == value) return;
                _avatarImage = value;
                OnPropertyChanged();
            }
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class CharaVaultViewModel : ViewModelBase
    {
        private static readonly HttpClient _http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
            return client;
        }

        private const string DefaultBaseUrl = "https://charavault.net";
        private const int PageSize = 25;

        private ObservableCollection<CharaVaultCardItem> _results = new ObservableCollection<CharaVaultCardItem>();
        private string _searchQuery = "";
        private bool _isLoading = false;
        private string _statusText = "";
        private int _currentOffset = 0;
        private bool _hasMore = false;
        private bool _showNsfw = false;

        public ObservableCollection<CharaVaultCardItem> Results
        {
            get => _results;
            set => Set(ref _results, value);
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set => Set(ref _searchQuery, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => Set(ref _statusText, value);
        }

        public bool HasMore
        {
            get => _hasMore;
            set => Set(ref _hasMore, value);
        }

        public bool ShowNsfw
        {
            get => _showNsfw;
            set => Set(ref _showNsfw, value);
        }

        public string GetBaseUrl()
        {
            var custom = App.Settings.GetCharaVaultUrl();
            return !string.IsNullOrWhiteSpace(custom) ? custom.TrimEnd('/') : DefaultBaseUrl;
        }

        public void Load()
        {
            var baseUrl = GetBaseUrl();
            try
            {
                StatusText = baseUrl == DefaultBaseUrl
                    ? "Search for characters on CharaVault"
                    : $"Search on {new Uri(baseUrl).Host}";
            }
            catch
            {
                StatusText = "Search for characters";
            }
        }

        public async Task SearchAsync()
        {
            if (IsLoading) return;
            _currentOffset = 0;
            Results.Clear();
            await LoadPageAsync();
        }

        public async Task LoadMoreAsync()
        {
            if (IsLoading || !HasMore) return;
            _currentOffset += PageSize;
            await LoadPageAsync();
        }

        private async Task LoadPageAsync()
        {
            IsLoading = true;
            StatusText = "Searching…";
            try
            {
                var baseUrl = GetBaseUrl();
                var token   = App.Settings.GetCharaVaultToken();
                var q       = Uri.EscapeDataString(SearchQuery ?? "");

                // CharaVault.net API — GET /api/cards
                var nsfw = _showNsfw ? "true" : "false";
                var url = $"{baseUrl}/api/cards?q={q}&limit={PageSize}&offset={_currentOffset}&sort=most_downloaded&nsfw={nsfw}";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("Accept", "application/json");

                // App passwords are prefixed cv_ per the developer docs
                if (!string.IsNullOrEmpty(token))
                    request.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var root = JObject.Parse(json);

                // CharaVault response: { cards: [...], total: N }
                // Fallback to common alternate shapes
                var nodes = root["results"] as JArray
                         ?? root["cards"] as JArray
                         ?? root["data"]?["nodes"] as JArray
                         ?? root["nodes"] as JArray;

                if (nodes == null || nodes.Count == 0)
                {
                    StatusText = _currentOffset == 0 ? "No results found." : "No more results.";
                    HasMore = false;
                    return;
                }

                var items = new System.Collections.Generic.List<CharaVaultCardItem>();

                foreach (var n in nodes)
                {
                    var folder = n["folder"]?.ToString() ?? "";
                    var file   = n["file"]?.ToString() ?? "";
                    var path   = !string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(file)
                                 ? $"{folder}/{file}"
                                 : n["path"]?.ToString() ?? n["fullPath"]?.ToString() ?? n["id"]?.ToString() ?? "";

                    var name    = n["name"]?.ToString()
                               ?? (!string.IsNullOrEmpty(file) ? System.IO.Path.GetFileNameWithoutExtension(file) : "Unknown");
                    var creator = n["creator"]?.ToString() ?? folder;

                    // Thumbnail served from /cards/thumb/{folder}/{file}
                    var thumbUrl = (!string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(file))
                                  ? $"{baseUrl}/cards/thumb/{folder}/{file}"
                                  : !string.IsNullOrEmpty(path)
                                  ? $"{baseUrl}/cards/thumb/{path}"
                                  : "";

                    items.Add(new CharaVaultCardItem
                    {
                        Id        = n["id"]?.ToString() ?? path,
                        FullPath  = path,
                        Name      = name,
                        Author    = creator,
                        Tagline   = n["description_preview"]?.ToString() ?? n["tagline"]?.ToString() ?? "",
                        AvatarUrl = thumbUrl,
                        Stars     = (int)(n["avg_rating"]?.Value<float>() ?? n["rating"]?.Value<float>() ?? 0f)
                    });
                }

                // Add results immediately so the UI is responsive
                foreach (var item in items)
                    Results.Add(item);

                // Load thumbnails in background — sequential, non-blocking on UI thread
                LoadThumbnailsInBackgroundAsync(items, token); // fire-and-forget

                var total = root["total"]?.Value<int>()
                         ?? root["data"]?["total"]?.Value<int>()
                         ?? root["count"]?.Value<int>()
                         ?? 0;
                HasMore = total > 0 && (_currentOffset + PageSize) < total;
                StatusText = total > 0 ? $"{Results.Count} of {total} results" : $"{Results.Count} results";
            }
            catch (Exception ex)
            {
                StatusText = "Error: " + ex.Message;
                HasMore = false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Loads thumbnails one-at-a-time on a background thread.  Never blocks the UI thread.
        /// Uses SetSourceAsync (non-blocking decode) when dispatching to the UI thread.
        /// </summary>
        private async void LoadThumbnailsInBackgroundAsync(
            System.Collections.Generic.List<CharaVaultCardItem> items, string token)
        {
            var dispatcher = CoreApplication.MainView.CoreWindow.Dispatcher;

            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item?.AvatarUrl)) continue;

                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, item.AvatarUrl);
                    if (!string.IsNullOrEmpty(token))
                        request.Headers.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                    // Download on thread pool — never capture UI context
                    var response = await _http.SendAsync(request).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode) continue;

                    var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                    // Dispatch to UI thread and await the full async operation before moving on
                    var tcs = new TaskCompletionSource<bool>();
                    var dispatched = dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        try
                        {
                            using (var ms = new MemoryStream(bytes))
                            {
                                var bmp = new BitmapImage();
                                await bmp.SetSourceAsync(ms.AsRandomAccessStream());
                                item.AvatarImage = bmp;
                            }
                            tcs.TrySetResult(true);
                        }
                        catch (Exception ex)
                        {
                            tcs.TrySetException(ex);
                        }
                    });

                    await tcs.Task.ConfigureAwait(false);
                }
                catch
                {
                    // Silent — initial-letter fallback shows instead
                }
            }
        }

        // Returns null on success, error message on failure
        public async Task<string> ImportCharacterAsync(CharaVaultCardItem item)
        {
            if (item == null) return "No item selected.";
            try
            {
                var baseUrl  = GetBaseUrl();
                var token    = App.Settings.GetCharaVaultToken();
                var fullPath = item.FullPath ?? item.Id;

                // CharaVault.net direct PNG download: GET /cards/{folder}/{file}
                var pngUrl = $"{baseUrl}/cards/{fullPath}";

                var request = new HttpRequestMessage(HttpMethod.Get, pngUrl);
                request.Headers.TryAddWithoutValidation("Accept", "image/png, image/*");
                if (!string.IsNullOrEmpty(token))
                    request.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    return $"URL: {pngUrl}\nHTTP {(int)response.StatusCode} — {body?.Substring(0, System.Math.Min(200, body.Length))}";
                }

                var bytes = await response.Content.ReadAsByteArrayAsync();
                await App.Characters.ImportCharacterFromBytesAsync(item.Name, bytes);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
