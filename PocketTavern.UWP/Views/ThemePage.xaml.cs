using System.Collections.ObjectModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.Models;
using PocketTavern.UWP.Services;

namespace PocketTavern.UWP.Views
{
    /// <summary>
    /// Binding wrapper for a theme entry shown in the theme list.
    /// </summary>
    public class ThemePageItem
    {
        public PocketTavernTheme Theme { get; }

        public string Name           => Theme.Name;
        public string ParticleHint   => BuildParticleHint();
        public Visibility IsActiveVisibility => Theme.Key == App.Theme.Current.Key
            ? Visibility.Visible : Visibility.Collapsed;

        // Color swatches for the preview strip
        public SolidColorBrush SwatchDeep      => new SolidColorBrush(Theme.BackgroundDeep);
        public SolidColorBrush SwatchSurface   => new SolidColorBrush(Theme.BackgroundSurface);
        public SolidColorBrush SwatchAccent    => new SolidColorBrush(Theme.AccentPrimary);
        public SolidColorBrush SwatchSecondary => new SolidColorBrush(Theme.TextPrimary);

        public ThemePageItem(PocketTavernTheme theme) { Theme = theme; }

        private string BuildParticleHint()
        {
            if (Theme.HasAudio && Theme.HasBackgroundImage)
                return "Animated background · Background music";
            if (Theme.HasAudio)
                return "Background music";
            if (Theme.HasBackgroundImage)
                return "Animated background";
            var p = Theme.ParticleEffect;
            if (p != null && p.Layers.Count > 0)
            {
                int total = 0;
                foreach (var l in p.Layers) total += l.Count;
                return $"Particle effects · {total} particles";
            }
            return "No particle effects";
        }
    }

    public sealed partial class ThemePage : Page
    {
        private readonly ObservableCollection<ThemePageItem> _items
            = new ObservableCollection<ThemePageItem>();

        public ThemePage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            Refresh();
        }

        private void Refresh()
        {
            _items.Clear();
            foreach (var theme in App.Theme.Available)
                _items.Add(new ThemePageItem(theme));
            ThemeList.ItemsSource = _items;
        }

        private void OnThemeClicked(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ThemePageItem item)
            {
                App.Theme.Apply(item.Theme);
                // Rebuild list so checkmarks update
                Refresh();
            }
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
            => App.Navigation.GoBack();
    }
}
