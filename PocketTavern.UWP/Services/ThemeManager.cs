using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Newtonsoft.Json.Linq;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.Services
{
    /// <summary>
    /// Loads PocketTavern theme JSON files and applies their colors to Application.Resources
    /// brushes at runtime.  All pages use StaticResource brushes by name; updating the
    /// brush Color property propagates automatically.
    /// </summary>
    public class ThemeManager
    {
        // ── Built-in default (matches Android app's hardcoded base colors) ────
        public static readonly PocketTavernTheme DefaultTheme = new PocketTavernTheme
        {
            Name             = "PocketTavern",
            Key              = "default",
            BackgroundDeep   = ColorFromHex("#0A0A0F"),
            BackgroundSurface= ColorFromHex("#12121A"),
            BackgroundCard   = ColorFromHex("#1A1A25"),
            AccentPrimary    = ColorFromHex("#FF6B00"),
            TextPrimary      = ColorFromHex("#EEEEEE"),
            TextSecondary    = ColorFromHex("#888888"),
            UserBubble       = ColorFromHex("#2A1200"),
            AiBubble         = ColorFromHex("#0A0F1A"),
            ParticleEffect   = null
        };

        private PocketTavernTheme _current = DefaultTheme;
        public  PocketTavernTheme Current => _current;

        // ── All available themes (loaded on Initialize) ───────────────────────
        private readonly List<PocketTavernTheme> _available = new List<PocketTavernTheme>();
        public  IReadOnlyList<PocketTavernTheme> Available => _available;

        // ── Audio player ──────────────────────────────────────────────────────
        private MediaPlayer _audioPlayer;

        // ── Initialization ────────────────────────────────────────────────────

        public async Task InitializeAsync(string savedKey)
        {
            _available.Clear();
            _available.Add(DefaultTheme);

            // Load bundled theme JSON files from the Assets\Themes folder
            try
            {
                var folder = await Package.Current.InstalledLocation
                    .GetFolderAsync(@"Assets\Themes");
                await LoadThemeFolderAsync(folder, "");
            }
            catch { /* folder may not exist in early builds */ }

            // Apply saved selection (fall back to default if not found)
            Apply(savedKey ?? "default");
        }

        private async Task LoadThemeFolderAsync(StorageFolder folder, string subKey)
        {
            foreach (var file in await folder.GetFilesAsync())
            {
                if (!file.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Sub-folder themes have their own folder (e.g. sand_and_sea\theme.json)
                bool isSub = !string.IsNullOrEmpty(subKey);
                string key = isSub ? subKey : System.IO.Path.GetFileNameWithoutExtension(file.Name);

                try
                {
                    var json = await FileIO.ReadTextAsync(file);
                    var theme = ParseThemeJson(json, key, isSub ? folder : null);
                    if (theme != null) _available.Add(theme);
                }
                catch { }
            }

            // Recurse into sub-folders (e.g. sand_and_sea/)
            foreach (var sub in await folder.GetFoldersAsync())
            {
                await LoadThemeFolderAsync(sub, sub.Name);
            }
        }

        // ── Parsing ───────────────────────────────────────────────────────────

        private static PocketTavernTheme ParseThemeJson(string json, string key, StorageFolder assetFolder)
        {
            var root = JObject.Parse(json);

            var theme = new PocketTavernTheme
            {
                Key              = key,
                Name             = root.Value<string>("name") ?? key,
                BackgroundDeep   = ParseRgba(root.Value<string>("shadow_color"),   DefaultTheme.BackgroundDeep),
                BackgroundSurface= ParseRgba(root.Value<string>("blur_tint_color"),DefaultTheme.BackgroundSurface),
                BackgroundCard   = ParseRgba(root.Value<string>("border_color"),   DefaultTheme.BackgroundCard),
                AccentPrimary    = ParseRgba(root.Value<string>("underline_text_color"), DefaultTheme.AccentPrimary),
                TextPrimary      = ParseRgba(root.Value<string>("main_text_color"),      DefaultTheme.TextPrimary),
                TextSecondary    = ParseRgba(root.Value<string>("quote_text_color"),     DefaultTheme.TextSecondary),
                UserBubble       = ParseRgba(root.Value<string>("user_mes_blur_tint_color"), DefaultTheme.UserBubble),
                AiBubble         = ParseRgba(root.Value<string>("bot_mes_blur_tint_color"),  DefaultTheme.AiBubble),

                HasBackgroundImage = root.Value<bool?>("background_image") ?? false,
                BackgroundOpacity  = root.Value<float?>("background_opacity") ?? 1f,
                HasLogo            = root.Value<bool?>("logo_image") ?? false,
                HasAudio           = root.Value<bool?>("theme_audio") ?? false,
                AudioLoop          = root.Value<bool?>("theme_audio_loop") ?? false,
            };

            if (assetFolder != null)
            {
                theme.BackgroundImagePath = System.IO.Path.Combine(assetFolder.Path, "background.gif");
                theme.LogoPath            = System.IO.Path.Combine(assetFolder.Path, "logo.png");
                theme.AudioPath           = System.IO.Path.Combine(assetFolder.Path, "music.mp3");
            }

            // Particle effect
            var particleToken = root["particle_effect"];
            if (particleToken != null)
            {
                theme.ParticleEffect = ParseParticleEffect(particleToken as JObject);
            }

            return theme;
        }

        private static ParticleEffectConfig ParseParticleEffect(JObject obj)
        {
            if (obj == null) return null;
            var cfg = new ParticleEffectConfig
            {
                AnimationDuration    = obj.Value<int?>("animation_duration") ?? 10000,
                BackgroundGlow       = obj.Value<bool?>("background_glow") ?? false,
                BackgroundGlowOpacity= obj.Value<float?>("background_glow_opacity") ?? 0f,
            };

            var layers = obj["layers"] as JArray;
            if (layers != null)
            {
                foreach (JObject l in layers)
                {
                    var layer = new ParticleLayer
                    {
                        Count           = l.Value<int?>("count")            ?? 20,
                        Shape           = l.Value<string>("shape")          ?? "circle",
                        Direction       = l.Value<string>("direction")      ?? "up",
                        SizeMin         = l.Value<float?>("size_min")       ?? 2f,
                        SizeMax         = l.Value<float?>("size_max")       ?? 5f,
                        SpeedMin        = l.Value<float?>("speed_min")      ?? 0.3f,
                        SpeedMax        = l.Value<float?>("speed_max")      ?? 0.7f,
                        WobbleAmplitude = l.Value<float?>("wobble_amplitude")  ?? 0.5f,
                        WobbleFrequency = l.Value<float?>("wobble_frequency")  ?? 1f,
                        OpacityMin      = l.Value<float?>("opacity_min")    ?? 0.2f,
                        OpacityMax      = l.Value<float?>("opacity_max")    ?? 0.6f,
                        Glow            = l.Value<bool?>("glow")            ?? false,
                        GlowRadius      = l.Value<float?>("glow_radius")    ?? 2f,
                        GlowOpacity     = l.Value<float?>("glow_opacity")   ?? 0.2f,
                        Rotation        = l.Value<bool?>("rotation")        ?? false,
                    };

                    var colors = l["colors"] as JArray;
                    if (colors != null)
                        foreach (var c in colors)
                            layer.Colors.Add(ColorFromHex(c.Value<string>()));

                    cfg.Layers.Add(layer);
                }
            }

            return cfg;
        }

        // ── Application ───────────────────────────────────────────────────────

        public void Apply(string key)
        {
            PocketTavernTheme theme = DefaultTheme;
            if (!string.IsNullOrEmpty(key) && key != "default")
            {
                foreach (var t in _available)
                    if (t.Key == key) { theme = t; break; }
            }
            _current = theme;
            ApplyToResources(theme);
            ApplyAudio(theme);
        }

        public void Apply(PocketTavernTheme theme)
        {
            _current = theme ?? DefaultTheme;
            ApplyToResources(_current);
            ApplyAudio(_current);
            App.Settings.SaveThemeKey(_current.Key);
        }

        private void ApplyAudio(PocketTavernTheme theme)
        {
            // Stop any currently playing audio first
            if (_audioPlayer != null)
            {
                try { _audioPlayer.Pause(); _audioPlayer.Dispose(); }
                catch { }
                _audioPlayer = null;
            }

            if (!theme.HasAudio || string.IsNullOrEmpty(theme.AudioPath) || !File.Exists(theme.AudioPath))
                return;

            try
            {
                _audioPlayer = new MediaPlayer();
                _audioPlayer.Source = Windows.Media.Core.MediaSource.CreateFromUri(
                    new Uri("file:///" + theme.AudioPath.Replace('\\', '/')));
                _audioPlayer.IsLoopingEnabled = theme.AudioLoop;
                _audioPlayer.Volume = 0.5;
                _audioPlayer.Play();
            }
            catch
            {
                _audioPlayer?.Dispose();
                _audioPlayer = null;
            }
        }

        private static void ApplyToResources(PocketTavernTheme theme)
        {
            var res = Application.Current.Resources;
            SetBrush(res, "BackgroundDeepBrush",    theme.BackgroundDeep);
            SetBrush(res, "BackgroundSurfaceBrush", theme.BackgroundSurface);
            SetBrush(res, "BackgroundCardBrush",    theme.BackgroundCard);
            SetBrush(res, "AccentPrimaryBrush",     theme.AccentPrimary);
            SetBrush(res, "TextPrimaryBrush",       theme.TextPrimary);
            SetBrush(res, "TextSecondaryBrush",     theme.TextSecondary);
            SetBrush(res, "UserBubbleBrush",        theme.UserBubble);
            SetBrush(res, "AiBubbleBrush",          theme.AiBubble);
            // AccentSecondaryBrush stays as a fixed ice-cyan unless the theme specifies it
        }

        private static void SetBrush(ResourceDictionary res, string key, Color color)
        {
            if (res.ContainsKey(key) && res[key] is SolidColorBrush brush)
                brush.Color = color;
        }

        // ── Color parsing helpers ─────────────────────────────────────────────

        /// <summary>Parses "rgba(r, g, b, a)" where a is 0..1, or falls back to default.</summary>
        public static Color ParseRgba(string rgba, Color fallback)
        {
            if (string.IsNullOrEmpty(rgba)) return fallback;
            try
            {
                rgba = rgba.Trim();
                if (rgba.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase))
                {
                    rgba = rgba.Substring(5).TrimEnd(')');
                    var parts = rgba.Split(',');
                    byte r = (byte)Math.Round(float.Parse(parts[0].Trim()));
                    byte g = (byte)Math.Round(float.Parse(parts[1].Trim()));
                    byte b = (byte)Math.Round(float.Parse(parts[2].Trim()));
                    byte a = (byte)Math.Round(float.Parse(parts[3].Trim(), System.Globalization.CultureInfo.InvariantCulture) * 255);
                    return Color.FromArgb(a, r, g, b);
                }
                if (rgba.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase))
                {
                    rgba = rgba.Substring(4).TrimEnd(')');
                    var parts = rgba.Split(',');
                    byte r = (byte)Math.Round(float.Parse(parts[0].Trim()));
                    byte g = (byte)Math.Round(float.Parse(parts[1].Trim()));
                    byte b = (byte)Math.Round(float.Parse(parts[2].Trim()));
                    return Color.FromArgb(255, r, g, b);
                }
                return ColorFromHex(rgba);
            }
            catch { return fallback; }
        }

        public static Color ColorFromHex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Colors.Transparent;
            hex = hex.TrimStart('#');
            try
            {
                if (hex.Length == 6)
                    return Color.FromArgb(255,
                        Convert.ToByte(hex.Substring(0, 2), 16),
                        Convert.ToByte(hex.Substring(2, 2), 16),
                        Convert.ToByte(hex.Substring(4, 2), 16));
                if (hex.Length == 8)
                    return Color.FromArgb(
                        Convert.ToByte(hex.Substring(0, 2), 16),
                        Convert.ToByte(hex.Substring(2, 2), 16),
                        Convert.ToByte(hex.Substring(4, 2), 16),
                        Convert.ToByte(hex.Substring(6, 2), 16));
            }
            catch { }
            return Colors.Transparent;
        }
    }
}
