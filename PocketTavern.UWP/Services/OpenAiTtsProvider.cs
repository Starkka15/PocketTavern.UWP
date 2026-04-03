using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Streams;

namespace PocketTavern.UWP.Services
{
    public class OpenAiTtsProvider
    {
        public string ApiUrl { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "tts-1";

        private MediaPlayer _player;
        private bool _speaking;

        public async Task SpeakAsync(string text, string voiceId, float speed)
        {
            if (string.IsNullOrWhiteSpace(ApiUrl)) return;
            Stop();

            var voice = !string.IsNullOrWhiteSpace(voiceId) ? voiceId : "alloy";
            var url = $"{ApiUrl.TrimEnd('/')}/v1/audio/speech";

            var jsonBody = $"{{\"model\":\"{Model}\",\"input\":{EscapeJson(text)},\"voice\":\"{voice}\",\"speed\":{speed}}}";

            try
            {
                using (var client = new HttpClient())
                {
                    if (!string.IsNullOrWhiteSpace(ApiKey))
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
                    client.DefaultRequestHeaders.Add("User-Agent", "PocketTavern/1.0");

                    var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(url, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync();
                        DebugLogger.Log($"[OpenAiTTS] API error: {(int)response.StatusCode} {errorBody}");
                        return;
                    }

                    var audioBytes = await response.Content.ReadAsByteArrayAsync();
                    if (audioBytes == null || audioBytes.Length == 0) return;

                    // Write to temp file
                    var tempFolder = ApplicationData.Current.TemporaryFolder;
                    var tempFile = await tempFolder.CreateFileAsync(
                        $"tts_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.mp3",
                        CreationCollisionOption.ReplaceExisting);
                    await FileIO.WriteBytesAsync(tempFile, audioBytes);

                    // Play via MediaPlayer
                    _speaking = true;
                    _player = new MediaPlayer();
                    _player.Source = Windows.Media.Core.MediaSource.CreateFromStorageFile(tempFile);
                    _player.MediaEnded += (s, e) =>
                    {
                        _speaking = false;
                        _player.Dispose();
                        _player = null;
                        try { tempFile.DeleteAsync().AsTask().Wait(); } catch { }
                    };
                    _player.MediaFailed += (s, e) =>
                    {
                        DebugLogger.Log($"[OpenAiTTS] MediaPlayer error: {e.ErrorMessage}");
                        _speaking = false;
                        _player.Dispose();
                        _player = null;
                        try { tempFile.DeleteAsync().AsTask().Wait(); } catch { }
                    };
                    _player.Play();

                    // Wait for completion
                    while (_speaking)
                        await Task.Delay(100);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[OpenAiTTS] Speak failed: {ex.Message}");
                _speaking = false;
            }
        }

        public void Stop()
        {
            try { _player?.Pause(); _player?.Dispose(); } catch { }
            _player = null;
            _speaking = false;
        }

        public async Task<List<TtsVoice>> GetVoicesAsync()
        {
            if (!string.IsNullOrWhiteSpace(ApiUrl))
            {
                try
                {
                    var serverVoices = await FetchVoicesFromServerAsync();
                    if (serverVoices.Count > 0) return serverVoices;
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[OpenAiTTS] Voice fetch failed: {ex.Message}");
                }
            }
            return DefaultVoices;
        }

        private async Task<List<TtsVoice>> FetchVoicesFromServerAsync()
        {
            var baseUrl = ApiUrl.TrimEnd('/');
            var paths = new[] { "/v1/audio/voices", "/v1/voices" };
            string body = null;

            using (var client = new HttpClient())
            {
                if (!string.IsNullOrWhiteSpace(ApiKey))
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
                client.DefaultRequestHeaders.Add("User-Agent", "PocketTavern/1.0");

                foreach (var path in paths)
                {
                    try
                    {
                        var response = await client.GetAsync($"{baseUrl}{path}");
                        if (response.IsSuccessStatusCode)
                        {
                            body = await response.Content.ReadAsStringAsync();
                            if (!string.IsNullOrWhiteSpace(body)) break;
                        }
                    }
                    catch { }
                }
            }

            if (string.IsNullOrWhiteSpace(body)) return new List<TtsVoice>();

            // Parse JSON — handles both {"voices": ["name1", "name2"]} and {"voices": [{"id":"...","name":"..."}]}
            var voices = new List<TtsVoice>();
            try
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(body);
                var arr = json["voices"] as Newtonsoft.Json.Linq.JArray;
                if (arr == null) return voices;

                foreach (var elem in arr)
                {
                    if (elem is Newtonsoft.Json.Linq.JValue val)
                    {
                        var name = (string)val;
                        if (!string.IsNullOrEmpty(name))
                            voices.Add(new TtsVoice { Id = name, Name = name });
                    }
                    else if (elem is Newtonsoft.Json.Linq.JObject obj)
                    {
                        var id = obj.Value<string>("id") ?? obj.Value<string>("voice_id");
                        if (id == null) continue;
                        var displayName = obj.Value<string>("name") ?? id;
                        voices.Add(new TtsVoice { Id = id, Name = displayName });
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[OpenAiTTS] Voice parse error: {ex.Message}");
            }
            return voices;
        }

        private static string EscapeJson(string text)
        {
            var sb = new StringBuilder("\"");
            foreach (var c in text)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            sb.Append("\"");
            return sb.ToString();
        }

        private static readonly List<TtsVoice> DefaultVoices = new List<TtsVoice>
        {
            new TtsVoice { Id = "alloy", Name = "Alloy" },
            new TtsVoice { Id = "echo", Name = "Echo" },
            new TtsVoice { Id = "fable", Name = "Fable" },
            new TtsVoice { Id = "onyx", Name = "Onyx" },
            new TtsVoice { Id = "nova", Name = "Nova" },
            new TtsVoice { Id = "shimmer", Name = "Shimmer" }
        };
    }
}
