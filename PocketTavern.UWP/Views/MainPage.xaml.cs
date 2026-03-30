using System;
using System.Collections.Generic;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using PocketTavern.UWP.Models;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class MainPage : Page
    {
        private readonly MainViewModel _vm = new MainViewModel();

        // ── Particle system ───────────────────────────────────────────────────
        private readonly List<ParticleState> _particles = new List<ParticleState>();
        private readonly DispatcherTimer _particleTimer = new DispatcherTimer();
        private readonly Random _rng = new Random();
        private DateTime _startTime;

        public MainPage()
        {
            this.InitializeComponent();
            _particleTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 fps
            _particleTimer.Tick += OnParticleTick;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await _vm.LoadAsync();
            ConnectionLabel.Text = _vm.ApiDisplayName;
            ApplyThemeAssets();

            // Initialize JS extension sandbox (once per app lifetime)
            App.Extensions.Initialize(ExtSandbox);
            await App.Extensions.LoadAsync();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _particleTimer.Stop();
            ParticleCanvas.SizeChanged -= OnCanvasSizeChanged;
        }

        // ── Theme assets ──────────────────────────────────────────────────────

        private void ApplyThemeAssets()
        {
            var theme = App.Theme.Current;

            // Background image (sand_and_sea animated GIF etc.)
            if (theme.HasBackgroundImage && System.IO.File.Exists(theme.BackgroundImagePath ?? ""))
            {
                try
                {
                    var bgUri = new Uri("file:///" + theme.BackgroundImagePath.Replace('\\', '/'));
                    ThemeBackground.Source  = new BitmapImage(bgUri);
                    ThemeBackground.Opacity = theme.BackgroundOpacity;
                    ThemeBackground.Visibility = Visibility.Visible;
                }
                catch { ThemeBackground.Visibility = Visibility.Collapsed; }
            }
            else
            {
                ThemeBackground.Visibility = Visibility.Collapsed;
            }

            // Custom logo
            if (theme.HasLogo && System.IO.File.Exists(theme.LogoPath ?? ""))
            {
                try
                {
                    var logoUri = new Uri("file:///" + theme.LogoPath.Replace('\\', '/'));
                    ThemeLogo.Source       = new BitmapImage(logoUri);
                    ThemeLogo.Visibility   = Visibility.Visible;
                    DefaultLogo.Visibility = Visibility.Collapsed;
                }
                catch
                {
                    ThemeLogo.Source     = new Windows.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/logo_pockettavern.png"));
                    ThemeLogo.Visibility   = Visibility.Visible;
                    DefaultLogo.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                ThemeLogo.Source     = new Windows.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/logo_pockettavern.png"));
                ThemeLogo.Visibility   = Visibility.Visible;
                DefaultLogo.Visibility = Visibility.Collapsed;
            }

            // Particles
            _particleTimer.Stop();
            ParticleCanvas.Children.Clear();
            _particles.Clear();

            if (theme.ParticleEffect?.Layers?.Count > 0)
            {
                _startTime = DateTime.UtcNow;
                if (ParticleCanvas.ActualWidth > 0)
                    SpawnParticles(theme.ParticleEffect);
                else
                    ParticleCanvas.SizeChanged += OnCanvasSizeChanged;
            }
        }

        private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width <= 0) return;
            ParticleCanvas.SizeChanged -= OnCanvasSizeChanged;
            var effect = App.Theme.Current.ParticleEffect;
            if (effect != null && _particles.Count == 0)
                SpawnParticles(effect);
        }

        private void SpawnParticles(ParticleEffectConfig effect)
        {
            double w = ParticleCanvas.ActualWidth;
            double h = ParticleCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            foreach (var layer in effect.Layers)
            {
                for (int i = 0; i < layer.Count; i++)
                {
                    var color = layer.Colors.Count > 0
                        ? layer.Colors[_rng.Next(layer.Colors.Count)]
                        : Colors.White;

                    double size      = layer.SizeMin + _rng.NextDouble() * (layer.SizeMax - layer.SizeMin);
                    double speed     = layer.SpeedMin + _rng.NextDouble() * (layer.SpeedMax - layer.SpeedMin);
                    double pxPerTick = speed * h / 60.0;
                    double opacity   = layer.OpacityMin + _rng.NextDouble() * (layer.OpacityMax - layer.OpacityMin);

                    double startX = _rng.NextDouble() * w;
                    double startY = _rng.NextDouble() * h;

                    var ellipse = new Ellipse
                    {
                        Width  = size,
                        Height = size,
                        Fill   = new SolidColorBrush(color) { Opacity = opacity }
                    };
                    Canvas.SetLeft(ellipse, startX);
                    Canvas.SetTop(ellipse,  startY);
                    ParticleCanvas.Children.Add(ellipse);

                    _particles.Add(new ParticleState
                    {
                        Shape        = ellipse,
                        X            = startX,
                        Y            = startY,
                        SpeedPx      = pxPerTick,
                        Direction    = layer.Direction,
                        WobbleAmp    = layer.WobbleAmplitude * Math.Min(w, h) * 0.04,
                        WobbleFreq   = layer.WobbleFrequency,
                        WobblePhase  = _rng.NextDouble() * Math.PI * 2,
                        Size         = size
                    });
                }
            }

            _particleTimer.Start();
        }

        private void OnParticleTick(object sender, object e)
        {
            double w = ParticleCanvas.ActualWidth;
            double h = ParticleCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            double t = (DateTime.UtcNow - _startTime).TotalSeconds;

            foreach (var p in _particles)
            {
                switch (p.Direction)
                {
                    case "up":   p.Y -= p.SpeedPx; break;
                    case "down": p.Y += p.SpeedPx; break;
                    default:     p.Y -= p.SpeedPx * 0.5; break;
                }

                double wx = p.X + Math.Sin(t * p.WobbleFreq + p.WobblePhase) * p.WobbleAmp;

                // Wrap vertically
                if (p.Y + p.Size < 0) p.Y = h + p.Size;
                if (p.Y > h + p.Size) p.Y = -p.Size;
                // Wrap horizontally
                if (wx < -p.Size) wx += w;
                if (wx > w)       wx -= w;

                Canvas.SetLeft(p.Shape, wx);
                Canvas.SetTop(p.Shape,  p.Y);
            }
        }

        // ── Navigation ────────────────────────────────────────────────────────

        private void OnCharactersClick(object sender, RoutedEventArgs e)
            => App.Navigation.NavigateToCharacters();

        private void OnRecentChatsClick(object sender, RoutedEventArgs e)
            => App.Navigation.NavigateToRecentChats();

        private void OnCreateCharacterClick(object sender, RoutedEventArgs e)
            => App.Navigation.NavigateToCreateCharacter();

        private void OnCharaVaultClick(object sender, RoutedEventArgs e)
            => App.Navigation.NavigateToCharaVault();

        private void OnSettingsClick(object sender, RoutedEventArgs e)
            => App.Navigation.NavigateToSettings();
    }

    internal sealed class ParticleState
    {
        public UIElement Shape       { get; set; }
        public double    X           { get; set; }
        public double    Y           { get; set; }
        public double    SpeedPx     { get; set; }
        public string    Direction   { get; set; }
        public double    WobbleAmp   { get; set; }
        public double    WobbleFreq  { get; set; }
        public double    WobblePhase { get; set; }
        public double    Size        { get; set; }
    }
}
