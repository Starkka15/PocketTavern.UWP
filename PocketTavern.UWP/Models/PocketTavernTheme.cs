using System.Collections.Generic;
using Windows.UI;

namespace PocketTavern.UWP.Models
{
    /// <summary>
    /// Parsed representation of a PocketTavern theme JSON file.
    /// Color fields are resolved to Windows.UI.Color after parsing the rgba() strings.
    /// </summary>
    public class PocketTavernTheme
    {
        // ── Identity ──────────────────────────────────────────────────────────
        public string Name { get; set; }
        public string Key  { get; set; }   // filename stem, e.g. "fire_and_ice"

        // ── Colors ────────────────────────────────────────────────────────────
        public Color BackgroundDeep    { get; set; }  // shadow_color
        public Color BackgroundSurface { get; set; }  // blur_tint_color
        public Color BackgroundCard    { get; set; }  // border_color

        public Color AccentPrimary   { get; set; }    // underline_text_color
        public Color TextPrimary     { get; set; }    // main_text_color
        public Color TextSecondary   { get; set; }    // quote_text_color

        public Color UserBubble { get; set; }   // user_mes_blur_tint_color
        public Color AiBubble   { get; set; }   // bot_mes_blur_tint_color

        // ── Assets ────────────────────────────────────────────────────────────
        public bool   HasBackgroundImage   { get; set; }
        public string BackgroundImagePath  { get; set; }  // null or absolute path
        public float  BackgroundOpacity    { get; set; } = 1f;

        public bool   HasLogo     { get; set; }
        public string LogoPath    { get; set; }

        public bool   HasAudio    { get; set; }
        public string AudioPath   { get; set; }
        public bool   AudioLoop   { get; set; }

        // ── Particles ─────────────────────────────────────────────────────────
        public ParticleEffectConfig ParticleEffect { get; set; }
    }

    public class ParticleEffectConfig
    {
        public List<ParticleLayer> Layers          { get; set; } = new List<ParticleLayer>();
        public int                 AnimationDuration { get; set; } = 10000;
        public bool                BackgroundGlow   { get; set; }
        public float               BackgroundGlowOpacity { get; set; }
    }

    public class ParticleLayer
    {
        public int     Count    { get; set; }
        public string  Shape    { get; set; }   // circle, snowflake, star, diamond
        public string  Direction{ get; set; }   // up, down, random
        public float   SizeMin  { get; set; }
        public float   SizeMax  { get; set; }
        public float   SpeedMin { get; set; }
        public float   SpeedMax { get; set; }
        public float   WobbleAmplitude  { get; set; }
        public float   WobbleFrequency  { get; set; }
        public float   OpacityMin { get; set; }
        public float   OpacityMax { get; set; }
        public bool    Glow      { get; set; }
        public float   GlowRadius{ get; set; }
        public float   GlowOpacity{ get; set; }
        public bool    Rotation  { get; set; }
        public List<Color> Colors { get; set; } = new List<Color>();
    }
}
