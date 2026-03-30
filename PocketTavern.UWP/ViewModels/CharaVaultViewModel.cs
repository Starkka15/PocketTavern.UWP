using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace PocketTavern.UWP.ViewModels
{
    public class CharaVaultCardItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public string Tagline { get; set; }
        public string AvatarUrl { get; set; }
        public int Stars { get; set; }
        public string FullPath { get; set; }
        public string Initial => Name?.Length > 0 ? Name[0].ToString().ToUpper() : "?";
    }

    public class CharaVaultViewModel : ViewModelBase
    {
        private static readonly HttpClient _http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "PocketTavern/1.0");
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

        private string GetBaseUrl()
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
                var url = $"{baseUrl}/api/cards?q={q}&limit={PageSize}&offset={_currentOffset}&sort=most_downloaded&nsfw=false";

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
                var nodes = root["cards"] as JArray
                         ?? root["data"]?["nodes"] as JArray
                         ?? root["nodes"] as JArray
                         ?? root["results"] as JArray;

                if (nodes == null || nodes.Count == 0)
                {
                    StatusText = _currentOffset == 0 ? "No results found." : "No more results.";
                    HasMore = false;
                    return;
                }

                foreach (var n in nodes)
                {
                    // Reconstruct folder/file path from whatever the API returns
                    var folder = n["folder"]?.ToString() ?? "";
                    var file   = n["file"]?.ToString() ?? "";
                    var path   = n["path"]?.ToString() ?? n["fullPath"]?.ToString() ?? "";

                    // If API returns separate folder+file fields, prefer those
                    if (!string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(file))
                        path = $"{folder}/{file}";
                    else if (string.IsNullOrEmpty(path))
                        path = n["id"]?.ToString() ?? "";

                    var parts   = path.Split('/');
                    var creator = !string.IsNullOrEmpty(folder) ? folder
                                : parts.Length >= 1 ? parts[0]
                                : (n["creator"]?.ToString() ?? "");
                    var name    = n["name"]?.ToString()
                               ?? (!string.IsNullOrEmpty(file) ? file.Replace('_', ' ')
                               : parts.Length >= 2 ? parts[1].Replace('_', ' ') : "Unknown");

                    Results.Add(new CharaVaultCardItem
                    {
                        Id        = n["id"]?.ToString() ?? path,
                        FullPath  = path,
                        Name      = name,
                        Author    = creator,
                        Tagline   = n["tagline"]?.ToString() ?? n["description"]?.ToString() ?? "",
                        AvatarUrl = n["avatar_url"]?.ToString() ?? "",
                        Stars     = n["rating"]?.Value<int>() ?? n["starCount"]?.Value<int>() ?? 0
                    });
                }

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
