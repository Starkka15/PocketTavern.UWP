using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PocketTavern.UWP.Data;
using PocketTavern.UWP.Models;
using PocketTavern.UWP.Services;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace PocketTavern.UWP.ViewModels
{
    /// <summary>
    /// Mirrors Android StImportViewModel.
    /// Imports characters, lorebooks, and chats from either:
    ///  - A local SillyTavern data folder (picked via FolderPicker)
    ///  - A running SillyTavern server over HTTP
    /// </summary>
    public class StImportViewModel : ViewModelBase
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        private readonly LoreBookStorage _lorebookStorage = new LoreBookStorage();

        // ── State ──────────────────────────────────────────────────────────────

        private string _serverUrl = "";
        private string _username = "";
        private string _password = "";
        private bool _isImporting = false;
        private bool _isComplete = false;
        private int _currentProgress = 0;
        private int _totalProgress = 0;
        private string _currentItem = "";
        private int _charactersImported = 0;
        private int _lorebooksImported = 0;
        private int _chatsImported = 0;
        private int _errors = 0;

        public string ServerUrl { get => _serverUrl; set => Set(ref _serverUrl, value); }
        public string Username  { get => _username;  set => Set(ref _username,  value); }
        public string Password  { get => _password;  set => Set(ref _password,  value); }

        public bool IsImporting   { get => _isImporting;   set => Set(ref _isImporting,   value); }
        public bool IsComplete    { get => _isComplete;     set => Set(ref _isComplete,    value); }
        public int CurrentProgress { get => _currentProgress; set => Set(ref _currentProgress, value); }
        public int TotalProgress   { get => _totalProgress;   set => Set(ref _totalProgress,   value); }
        public string CurrentItem  { get => _currentItem;     set => Set(ref _currentItem,     value); }
        public int CharactersImported { get => _charactersImported; set => Set(ref _charactersImported, value); }
        public int LorebooksImported  { get => _lorebooksImported;  set => Set(ref _lorebooksImported,  value); }
        public int ChatsImported      { get => _chatsImported;      set => Set(ref _chatsImported,      value); }
        public int Errors             { get => _errors;              set => Set(ref _errors,             value); }

        public ObservableCollection<string> Log { get; } = new ObservableCollection<string>();

        // ── Reset ──────────────────────────────────────────────────────────────

        public void ResetState()
        {
            IsImporting = false;
            IsComplete = false;
            CurrentProgress = 0;
            TotalProgress = 0;
            CurrentItem = "";
            CharactersImported = 0;
            LorebooksImported = 0;
            ChatsImported = 0;
            Errors = 0;
            Log.Clear();
        }

        // ── Folder Import ──────────────────────────────────────────────────────

        /// <summary>
        /// Opens a folder picker and imports ST data from the selected folder.
        /// Looks for characters/, worlds/, chats/ sub-folders.
        /// </summary>
        public async Task ImportFromFolderAsync()
        {
            StorageFolder folder;
            try
            {
                var picker = new FolderPicker();
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add("*");
                folder = await picker.PickSingleFolderAsync();
                if (folder == null) return;
            }
            catch (Exception ex)
            {
                AddLog("ERROR: Could not open folder picker: " + ex.Message);
                return;
            }

            IsImporting = true;
            IsComplete = false;
            Log.Clear();
            int chars = 0, lorebooks = 0, chats = 0, errors = 0;

            try
            {
                // Characters
                var charsFolder = await TryGetFolderAsync(folder, "characters");
                if (charsFolder != null)
                {
                    var pngs = (await charsFolder.GetFilesAsync())
                        .Where(f => f.FileType.Equals(".png", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    AddLog($"Found {pngs.Count} character PNG(s)");
                    for (int i = 0; i < pngs.Count; i++)
                    {
                        UpdateProgress(i + 1, pngs.Count, pngs[i].Name);
                        try
                        {
                            var bytes = await ReadFileBytesAsync(pngs[i]);
                            await App.Characters.ImportCharacterFromBytesAsync(pngs[i].Name, bytes);
                            chars++;
                            AddLog("Imported character: " + pngs[i].Name);
                        }
                        catch (Exception ex)
                        {
                            errors++;
                            AddLog($"ERROR importing {pngs[i].Name}: {ex.Message}");
                        }
                    }
                }
                else
                {
                    AddLog("No characters/ folder found");
                }

                // Lorebooks
                var worldsFolder = await TryGetFolderAsync(folder, "worlds")
                    ?? await TryGetFolderAsync(folder, "world_info");
                if (worldsFolder != null)
                {
                    var books = (await worldsFolder.GetFilesAsync())
                        .Where(f => f.FileType.Equals(".json", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    AddLog($"Found {books.Count} lorebook(s)");
                    foreach (var book in books)
                    {
                        try
                        {
                            var text = await FileIO.ReadTextAsync(book);
                            var name = Path.GetFileNameWithoutExtension(book.Name);
                            var entries = ParseWorldInfoJson(text);
                            await _lorebookStorage.SaveLorebookAsync(name, entries);
                            lorebooks++;
                            AddLog($"Imported lorebook: {name} ({entries.Count} entries)");
                        }
                        catch (Exception ex)
                        {
                            errors++;
                            AddLog($"ERROR importing {book.Name}: {ex.Message}");
                        }
                    }
                }
                else
                {
                    AddLog("No worlds/ folder found");
                }

                // Chats
                var chatsFolder = await TryGetFolderAsync(folder, "chats");
                if (chatsFolder != null)
                {
                    var result = await ImportChatsFromFolderAsync(chatsFolder);
                    chats += result.Item1;
                    errors += result.Item2;
                }
                else
                {
                    AddLog("No chats/ folder found");
                }
            }
            catch (Exception ex)
            {
                errors++;
                AddLog("ERROR: " + ex.Message);
            }
            finally
            {
                FinishImport(chars, lorebooks, chats, errors);
            }
        }

        private async Task<Tuple<int, int>> ImportChatsFromFolderAsync(StorageFolder chatsFolder)
        {
            int imported = 0, errors = 0;
            var charDirs = await chatsFolder.GetFoldersAsync();
            foreach (var charDir in charDirs)
            {
                var chatFiles = (await charDir.GetFilesAsync())
                    .Where(f => f.FileType.Equals(".jsonl", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var destDir = Path.Combine(
                    ApplicationData.Current.LocalFolder.Path,
                    "chats", charDir.Name);
                Directory.CreateDirectory(destDir);

                foreach (var chatFile in chatFiles)
                {
                    try
                    {
                        var text = await FileIO.ReadTextAsync(chatFile);
                        var destPath = Path.Combine(destDir, chatFile.Name);
                        File.WriteAllText(destPath, text);
                        imported++;
                        AddLog($"Imported chat: {charDir.Name}/{chatFile.Name}");
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        AddLog($"ERROR importing chat: {ex.Message}");
                    }
                }
            }
            return Tuple.Create(imported, errors);
        }

        // ── Server Import ──────────────────────────────────────────────────────

        public async Task ImportFromServerAsync()
        {
            var baseUrl = ServerUrl?.Trim().TrimEnd('/') ?? "";
            if (string.IsNullOrEmpty(baseUrl))
            {
                AddLog("ERROR: Server URL is required");
                return;
            }

            IsImporting = true;
            IsComplete = false;
            Log.Clear();

            int chars = 0, lorebooks = 0, errors = 0;

            try
            {
                // 1. Fetch CSRF token and cookies
                var csrfResult = await FetchCsrfTokenAsync(baseUrl);
                var csrfToken = csrfResult.Item1; var sessionCookie = csrfResult.Item2;
                if (csrfToken == null)
                {
                    AddLog("ERROR: Could not fetch CSRF token from " + baseUrl);
                    FinishImport(errors: 1);
                    return;
                }
                AddLog("Connected to ST server");

                // 2. Login if credentials provided
                var handle = Username?.Trim() ?? "";
                var activeCookie = sessionCookie;
                if (!string.IsNullOrEmpty(handle))
                {
                    var loginCookie = await DoLoginAsync(baseUrl, csrfToken, activeCookie,
                        handle, Password?.Trim() ?? "");
                    if (loginCookie != null)
                    {
                        activeCookie = loginCookie;
                        AddLog("Logged in as " + handle);
                    }
                    else
                    {
                        AddLog("WARN: Login failed — proceeding anyway");
                    }
                }

                // 3. Import characters
                AddLog("Fetching character list...");
                var charList = await FetchCharacterListAsync(baseUrl, csrfToken, activeCookie);
                AddLog($"Found {charList.Count} character(s)");

                for (int i = 0; i < charList.Count; i++)
                {
                    var name = charList[i].Item1; var filename = charList[i].Item2;
                    UpdateProgress(i + 1, charList.Count, name);
                    try
                    {
                        var pngBytes = await ExportCharacterAsync(baseUrl, csrfToken, activeCookie,
                            name, filename);
                        if (pngBytes != null && pngBytes.Length > 0)
                        {
                            var safeName = string.IsNullOrEmpty(filename) ? $"{name}.png" : filename;
                            await App.Characters.ImportCharacterFromBytesAsync(safeName, pngBytes);
                            chars++;
                            AddLog("Imported: " + name);
                        }
                        else
                        {
                            errors++;
                            AddLog("ERROR: Empty response for " + name);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        AddLog($"ERROR importing {name}: {ex.Message}");
                    }
                }

                // 4. Import lorebooks
                AddLog("Fetching lorebook list...");
                var lorebookNames = await FetchLorebookListAsync(baseUrl, csrfToken, activeCookie);
                AddLog($"Found {lorebookNames.Count} lorebook(s)");

                foreach (var lbName in lorebookNames)
                {
                    try
                    {
                        var entries = await FetchLorebookAsync(baseUrl, csrfToken, activeCookie, lbName);
                        await _lorebookStorage.SaveLorebookAsync(lbName, entries);
                        lorebooks++;
                        AddLog($"Imported lorebook: {lbName} ({entries.Count} entries)");
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        AddLog($"ERROR importing lorebook {lbName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                errors++;
                AddLog("ERROR: " + ex.Message);
            }
            finally
            {
                FinishImport(chars: chars, lorebooks: lorebooks, errors: errors);
            }
        }

        // ── HTTP helpers ───────────────────────────────────────────────────────

        private async Task<Tuple<string, string>> FetchCsrfTokenAsync(string baseUrl)
        {
            try
            {
                var resp = await _http.GetAsync(baseUrl + "/csrf-token");
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    AddLog($"ERROR /csrf-token: HTTP {(int)resp.StatusCode}");
                    return Tuple.Create<string, string>(null, null);
                }

                // Capture Set-Cookie
                string cookies = null;
                if (resp.Headers.TryGetValues("Set-Cookie", out var setCookies))
                {
                    cookies = string.Join("; ", setCookies.Select(c => c.Split(';')[0].Trim()));
                }

                var obj = JObject.Parse(body);
                var token = obj.Value<string>("token");
                return Tuple.Create(token, cookies);
            }
            catch (Exception ex)
            {
                AddLog("ERROR /csrf-token: " + ex.Message);
                return Tuple.Create<string, string>(null, null);
            }
        }

        private async Task<string> DoLoginAsync(string baseUrl, string csrf, string cookie,
            string handle, string password)
        {
            try
            {
                var body = JsonConvert.SerializeObject(new { handle, password });
                var resp = await PostJsonAsync(baseUrl + "/api/users/login", body, csrf, cookie);
                if (resp.IsSuccessStatusCode)
                {
                    if (resp.Headers.TryGetValues("Set-Cookie", out var sc))
                        return string.Join("; ", sc.Select(c => c.Split(';')[0].Trim()));
                    return cookie;
                }
                if ((int)resp.StatusCode == 404)
                {
                    AddLog("INFO: ST single-user mode, no login needed");
                    return cookie;
                }
                AddLog($"WARN: Login returned HTTP {(int)resp.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                AddLog("WARN: Login failed: " + ex.Message);
                return null;
            }
        }

        private async Task<List<Tuple<string, string>>> FetchCharacterListAsync(
            string baseUrl, string csrf, string cookie)
        {
            try
            {
                var resp = await PostJsonAsync(baseUrl + "/api/characters/all", "{}", csrf, cookie);
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    AddLog($"ERROR /api/characters/all: HTTP {(int)resp.StatusCode}");
                    return new List<Tuple<string, string>>();
                }
                var arr = JArray.Parse(body);
                var result = new List<Tuple<string, string>>();
                foreach (var el in arr)
                {
                    var name = el.Value<string>("name");
                    var avatar = el.Value<string>("avatar") ?? $"{name}.png";
                    if (!string.IsNullOrEmpty(name))
                        result.Add(Tuple.Create(name, avatar));
                }
                return result;
            }
            catch (Exception ex)
            {
                AddLog("ERROR parsing character list: " + ex.Message);
                return new List<Tuple<string, string>>();
            }
        }

        private async Task<byte[]> ExportCharacterAsync(string baseUrl, string csrf, string cookie,
            string name, string avatarUrl)
        {
            var body = JsonConvert.SerializeObject(new { avatar_url = avatarUrl, format = "png" });
            var resp = await PostJsonAsync(baseUrl + "/api/characters/export", body, csrf, cookie);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                AddLog($"ERROR export {name}: HTTP {(int)resp.StatusCode} — {err.Substring(0, Math.Min(80, err.Length))}");
                return null;
            }
            return await resp.Content.ReadAsByteArrayAsync();
        }

        private async Task<List<string>> FetchLorebookListAsync(string baseUrl, string csrf, string cookie)
        {
            try
            {
                var resp = await PostJsonAsync(baseUrl + "/api/worldinfo/list", "{}", csrf, cookie);
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    AddLog($"ERROR /api/worldinfo/list: HTTP {(int)resp.StatusCode}");
                    return new List<string>();
                }
                var arr = JArray.Parse(body);
                return arr.Select(el => el.Value<string>("name"))
                    .Where(n => n != null).ToList();
            }
            catch (Exception ex)
            {
                AddLog("ERROR parsing lorebook list: " + ex.Message);
                return new List<string>();
            }
        }

        private async Task<List<WorldInfoEntry>> FetchLorebookAsync(string baseUrl, string csrf,
            string cookie, string name)
        {
            var body = JsonConvert.SerializeObject(new { name });
            var resp = await PostJsonAsync(baseUrl + "/api/worldinfo/get", body, csrf, cookie);
            var text = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                AddLog($"ERROR /api/worldinfo/get {name}: HTTP {(int)resp.StatusCode}");
                return new List<WorldInfoEntry>();
            }
            return ParseWorldInfoJson(text);
        }

        private async Task<HttpResponseMessage> PostJsonAsync(string url, string json,
            string csrfToken, string cookie)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            req.Headers.Add("X-CSRF-Token", csrfToken);
            if (!string.IsNullOrEmpty(cookie))
                req.Headers.Add("Cookie", cookie);
            return await _http.SendAsync(req);
        }

        // ── World info parser ──────────────────────────────────────────────────

        private static List<WorldInfoEntry> ParseWorldInfoJson(string text)
        {
            try
            {
                var obj = JObject.Parse(text);
                var entries = obj["entries"] as JObject;
                if (entries == null) return new List<WorldInfoEntry>();

                var result = new List<WorldInfoEntry>();
                foreach (var kv in entries)
                {
                    try
                    {
                        var e = kv.Value as JObject;
                        if (e == null) continue;
                        result.Add(new WorldInfoEntry
                        {
                            Uid           = e.Value<string>("uid") ?? "",
                            Keys          = e["key"]?.Values<string>().ToList() ?? new List<string>(),
                            SecondaryKeys = e["keysecondary"]?.Values<string>().ToList() ?? new List<string>(),
                            Content       = e.Value<string>("content") ?? "",
                            Comment       = e.Value<string>("comment") ?? "",
                            Constant      = e.Value<bool>("constant"),
                            Selective     = e.Value<bool>("selective"),
                            Order         = e.Value<int?>("order") ?? 100,
                            Position      = e.Value<int?>("position") ?? 0,
                            Depth         = e.Value<int?>("depth") ?? 4,
                            Probability   = e.Value<int?>("probability") ?? 100,
                            Enabled       = !(e.Value<bool?>("disable") ?? false),
                            Group         = e.Value<string>("group") ?? "",
                            ScanDepth     = e.Value<int?>("scan_depth"),
                            CaseSensitive = e.Value<bool?>("case_sensitive") ?? false,
                            MatchWholeWords = e.Value<bool?>("match_whole_words") ?? false
                        });
                    }
                    catch { }
                }
                return result;
            }
            catch { return new List<WorldInfoEntry>(); }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static async Task<byte[]> ReadFileBytesAsync(StorageFile file)
        {
            var buffer = await FileIO.ReadBufferAsync(file);
            var bytes = new byte[buffer.Length];
            Windows.Storage.Streams.DataReader.FromBuffer(buffer).ReadBytes(bytes);
            return bytes;
        }

        private static async Task<StorageFolder> TryGetFolderAsync(StorageFolder parent, string name)
        {
            try { return await parent.GetFolderAsync(name); }
            catch { return null; }
        }

        private void AddLog(string message)
        {
            Log.Add(message);
            DebugLogger.Log("[StImport] " + message);
        }

        private void UpdateProgress(int current, int total, string item)
        {
            CurrentProgress = current;
            TotalProgress = total;
            CurrentItem = item;
        }

        private void FinishImport(int chars = 0, int lorebooks = 0, int chats = 0, int errors = 0)
        {
            IsImporting = false;
            IsComplete = true;
            CharactersImported = chars;
            LorebooksImported = lorebooks;
            ChatsImported = chats;
            Errors = errors;
            AddLog($"Done. Characters: {chars}, Lorebooks: {lorebooks}, Chats: {chats}, Errors: {errors}");
        }
    }
}
