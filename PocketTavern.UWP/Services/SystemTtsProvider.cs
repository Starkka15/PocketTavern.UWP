using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.SpeechSynthesis;
using Windows.UI.Xaml.Controls;

namespace PocketTavern.UWP.Services
{
    public class SystemTtsProvider
    {
        private SpeechSynthesizer _synth;
        private bool _ready;
        private bool _speaking;
        private MediaElement _media;

        public SystemTtsProvider()
        {
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try
            {
                _synth = new SpeechSynthesizer();
                _ready = true;
                DebugLogger.Log("[SystemTTS] Initialized successfully");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[SystemTTS] Init failed: {ex.Message}");
            }
        }

        public async Task SpeakAsync(string text, string voiceId, float speed)
        {
            if (!_ready || _synth == null) return;
            if (string.IsNullOrWhiteSpace(text)) return;

            Stop();

            // Set voice if specified
            if (!string.IsNullOrEmpty(voiceId))
            {
                try
                {
                    var voices = SpeechSynthesizer.AllVoices;
                    var match = voices.FirstOrDefault(v => v.Id == voiceId || v.DisplayName == voiceId);
                    if (match != null) _synth.Voice = match;
                }
                catch { }
            }

            // Set speed (UWP uses SSML prosody rate)
            var ssml = speed != 1.0f
                ? $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>" +
                  $"<prosody rate='{speed:F1}'>{EscapeXml(text)}</prosody></speak>"
                : null;

            try
            {
                SpeechSynthesisStream stream;
                if (ssml != null)
                    stream = await _synth.SynthesizeSsmlToStreamAsync(ssml);
                else
                    stream = await _synth.SynthesizeTextToStreamAsync(text);

                _media = new MediaElement();
                _media.SetSource(stream, stream.ContentType);
                _speaking = true;
                _media.MediaEnded += (s, e) => { _speaking = false; };
                _media.MediaFailed += (s, e) => { _speaking = false; };
                _media.Play();

                // Wait for completion (simple polling for UWP)
                while (_speaking)
                    await Task.Delay(100);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[SystemTTS] Speak failed: {ex.Message}");
                _speaking = false;
            }
        }

        public void Stop()
        {
            try { _media?.Pause(); } catch { }
            _speaking = false;
        }

        public Task<List<TtsVoice>> GetVoicesAsync()
        {
            var result = new List<TtsVoice>();
            try
            {
                foreach (var voice in SpeechSynthesizer.AllVoices)
                {
                    result.Add(new TtsVoice
                    {
                        Id = voice.Id,
                        Name = voice.DisplayName,
                        Language = voice.Language
                    });
                }
            }
            catch { }
            return Task.FromResult(result);
        }

        public void Shutdown()
        {
            Stop();
            _synth?.Dispose();
            _synth = null;
            _ready = false;
        }

        private static string EscapeXml(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var sb = new System.Text.StringBuilder(text.Length);
            foreach (char c in text)
            {
                switch (c)
                {
                    case '&':  sb.Append("&amp;");  break;
                    case '<':  sb.Append("&lt;");   break;
                    case '>':  sb.Append("&gt;");   break;
                    case '"':  sb.Append("&quot;"); break;
                    case '\'': sb.Append("&apos;"); break;
                    default:   sb.Append(c);        break;
                }
            }
            return sb.ToString();
        }
    }
}
