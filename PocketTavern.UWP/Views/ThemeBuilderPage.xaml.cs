using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.Models;
using PocketTavern.UWP.Services;

namespace PocketTavern.UWP.Views
{
    public class ColorFieldViewModel
    {
        public string Key { get; set; }
        public string Label { get; set; }
    }

    public sealed partial class ThemeBuilderPage : Page
    {
        private PocketTavernTheme _previewTheme;
        private bool _suppressSliderEvent;

        private readonly List<ColorFieldViewModel> _fields = new List<ColorFieldViewModel>();
        private readonly ObservableCollection<ColorFieldViewModel> _fieldVms
            = new ObservableCollection<ColorFieldViewModel>();

        private static readonly Dictionary<string, string> FieldLabels = new Dictionary<string, string>
        {
            ["deep"] = "Background (deep)", ["surface"] = "Surface",
            ["card"] = "Cards", ["accent"] = "Accent",
            ["textPri"] = "Text (primary)", ["textSec"] = "Text (secondary)",
            ["userBubble"] = "User bubble", ["aiBubble"] = "AI bubble"
        };

        public ThemeBuilderPage() { this.InitializeComponent(); }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _previewTheme = new PocketTavernTheme
            {
                Name = "Preview",
                Key = "__preview__",
                BackgroundDeep = Color.FromArgb(255, 10, 10, 15),
                BackgroundSurface = Color.FromArgb(255, 18, 18, 26),
                BackgroundCard = Color.FromArgb(255, 26, 26, 37),
                AccentPrimary = Color.FromArgb(255, 255, 107, 0),
                TextPrimary = Color.FromArgb(255, 238, 238, 238),
                TextSecondary = Color.FromArgb(255, 136, 136, 136),
                UserBubble = Color.FromArgb(255, 42, 18, 0),
                AiBubble = Color.FromArgb(255, 10, 15, 26)
            };
            BuildFieldList();
            UpdateSwatches();
            ColorFieldList.SelectedIndex = 0;
        }

        private void BuildFieldList()
        {
            _fields.Clear();
            _fieldVms.Clear();
            foreach (var kv in FieldLabels)
            {
                var vm = new ColorFieldViewModel { Key = kv.Key, Label = kv.Value };
                _fields.Add(vm);
                _fieldVms.Add(vm);
            }
            ColorFieldList.ItemsSource = _fieldVms;
        }

        private Color GetFieldColor(string key) => key switch
        {
            "deep" => _previewTheme.BackgroundDeep,
            "surface" => _previewTheme.BackgroundSurface,
            "card" => _previewTheme.BackgroundCard,
            "accent" => _previewTheme.AccentPrimary,
            "textPri" => _previewTheme.TextPrimary,
            "textSec" => _previewTheme.TextSecondary,
            "userBubble" => _previewTheme.UserBubble,
            "aiBubble" => _previewTheme.AiBubble,
            _ => Colors.Transparent
        };

        private void SetFieldColor(string key, Color c)
        {
            switch (key)
            {
                case "deep": _previewTheme.BackgroundDeep = c; break;
                case "surface": _previewTheme.BackgroundSurface = c; break;
                case "card": _previewTheme.BackgroundCard = c; break;
                case "accent": _previewTheme.AccentPrimary = c; break;
                case "textPri": _previewTheme.TextPrimary = c; break;
                case "textSec": _previewTheme.TextSecondary = c; break;
                case "userBubble": _previewTheme.UserBubble = c; break;
                case "aiBubble": _previewTheme.AiBubble = c; break;
            }
        }

        private void OnBackClick(object sender, RoutedEventArgs e) => App.Navigation.GoBack();

        private void OnFieldSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ColorFieldList.SelectedItem is ColorFieldViewModel vm)
            {
                CurrentFieldLabel.Text = vm.Label;
                var c = GetFieldColor(vm.Key);
                _suppressSliderEvent = true;
                RedSlider.Value = c.R;
                GreenSlider.Value = c.G;
                BlueSlider.Value = c.B;
                AlphaSlider.Value = c.A;
                _suppressSliderEvent = false;
            }
        }

        private void OnSliderChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_suppressSliderEvent) return;
            if (ColorFieldList.SelectedItem is ColorFieldViewModel vm)
            {
                var c = Color.FromArgb(
                    (byte)AlphaSlider.Value,
                    (byte)RedSlider.Value,
                    (byte)GreenSlider.Value,
                    (byte)BlueSlider.Value);
                SetFieldColor(vm.Key, c);
                UpdateSwatches();
                ThemeManager.ApplyPreview(_previewTheme);
            }
        }

        private void UpdateSwatches()
        {
            var swatches = new Dictionary<string, Border>
            {
                ["deep"] = SwatchDeep, ["surface"] = SwatchSurface, ["card"] = SwatchCard,
                ["accent"] = SwatchAccent, ["textPri"] = SwatchTextPri, ["textSec"] = SwatchTextSec,
                ["userBubble"] = SwatchUserBubble, ["aiBubble"] = SwatchAiBubble
            };
            foreach (var kv in swatches)
            {
                var c = GetFieldColor(kv.Key);
                kv.Value.Background = new SolidColorBrush(c);
            }
        }

        private async void OnSaveClick(object sender, RoutedEventArgs e)
        {
            var name = ThemeNameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                var d = new ContentDialog
                {
                    Title = "Name required",
                    Content = "Enter a theme name before saving.",
                    CloseButtonText = "OK"
                };
                await d.ShowAsync();
                return;
            }

            var key = name.ToLowerInvariant().Replace(' ', '_');
            var themeObj = new JObject
            {
                ["name"] = name,
                ["shadow_color"] = ToRgba(_previewTheme.BackgroundDeep),
                ["blur_tint_color"] = ToRgba(_previewTheme.BackgroundSurface),
                ["border_color"] = ToRgba(_previewTheme.BackgroundCard),
                ["underline_text_color"] = ToRgba(_previewTheme.AccentPrimary),
                ["main_text_color"] = ToRgba(_previewTheme.TextPrimary),
                ["quote_text_color"] = ToRgba(_previewTheme.TextSecondary),
                ["user_mes_blur_tint_color"] = ToRgba(_previewTheme.UserBubble),
                ["bot_mes_blur_tint_color"] = ToRgba(_previewTheme.AiBubble)
            };

            var local = ApplicationData.Current.LocalFolder;
            var themesDir = await local.CreateFolderAsync("themes", CreationCollisionOption.OpenIfExists);
            var themeFolder = await themesDir.CreateFolderAsync(key, CreationCollisionOption.ReplaceExisting);
            var file = await themeFolder.CreateFileAsync("theme.json", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, themeObj.ToString(Formatting.Indented));

            await App.Theme.LoadUserThemeAsync(key, themeFolder);
            App.Theme.Apply(key);

            var done = new ContentDialog
            {
                Title = "Theme saved",
                Content = $"\"{name}\" has been created and applied.",
                CloseButtonText = "OK"
            };
            await done.ShowAsync();
            App.Navigation.GoBack();
        }

        private static string ToRgba(Color c)
            => $"rgba({c.R}, {c.G}, {c.B}, {c.A / 255.0:F2})";
    }
}
