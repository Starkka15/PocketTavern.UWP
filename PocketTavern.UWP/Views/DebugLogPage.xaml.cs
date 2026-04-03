using System;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.Controls;
using PocketTavern.UWP.Services;

namespace PocketTavern.UWP.Views
{
    public sealed partial class DebugLogPage : Page
    {
        public DebugLogPage() { this.InitializeComponent(); }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            DebugLogger.EntryAdded += OnEntryAdded;
            RebuildLog();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            DebugLogger.EntryAdded -= OnEntryAdded;
        }

        private async void OnEntryAdded(object sender, EventArgs e)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                RebuildLog();
            });
        }

        private void RebuildLog()
        {
            var entries = DebugLogger.GetEntries();
            LogPanel.Children.Clear();

            if (entries.Count == 0)
            {
                EmptyState.Visibility = Visibility.Visible;
                LogScroll.Visibility  = Visibility.Collapsed;
                return;
            }

            EmptyState.Visibility = Visibility.Collapsed;
            LogScroll.Visibility  = Visibility.Visible;

            foreach (var entry in entries)
            {
                Color color;
                if (entry.Level == LogLevel.Request)       color = Color.FromArgb(255, 86, 186, 233);
                else if (entry.Level == LogLevel.Response)  color = Color.FromArgb(255, 86, 233, 164);
                else if (entry.Level == LogLevel.Error)     color = Color.FromArgb(255, 207, 102, 121);
                else                                        color = Color.FromArgb(255, 180, 180, 180);

                var border = new Border
                {
                    Padding       = new Thickness(8, 6, 8, 6),
                    CornerRadius  = new CornerRadius(4),
                    Background    = (SolidColorBrush)Application.Current.Resources["BackgroundCardBrush"],
                    Margin        = new Thickness(0, 0, 0, 2)
                };

                var panel = new SpacedPanel { Spacing = 2 };

                // Timestamp + level
                var header = new TextBlock
                {
                    Text       = $"{entry.Timestamp:HH:mm:ss.fff}  {entry.Level}",
                    FontSize   = 10,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(color)
                };
                panel.Children.Add(header);

                // Message body
                var msg = new TextBlock
                {
                    Text         = entry.Message,
                    FontSize     = 11,
                    FontFamily   = new FontFamily("Consolas"),
                    Foreground   = (SolidColorBrush)Application.Current.Resources["TextPrimaryBrush"],
                    TextWrapping = TextWrapping.Wrap
                };
                panel.Children.Add(msg);

                border.Child = panel;
                LogPanel.Children.Add(border);
            }

            if (AutoScrollToggle.IsOn)
            {
                LogScroll.UpdateLayout();
                LogScroll.ChangeView(null, LogScroll.ScrollableHeight, null);
            }
        }

        private void OnBackClick(object sender, RoutedEventArgs e) => App.Navigation.GoBack();

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            DebugLogger.Clear();
            RebuildLog();
        }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            var text = DebugLogger.ExportText();
            var dp = new DataPackage();
            dp.SetText(text);
            Clipboard.SetContent(dp);
        }
    }
}
