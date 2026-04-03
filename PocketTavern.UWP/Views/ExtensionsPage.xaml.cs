using System;
using System.Collections.Generic;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using Windows.Storage.Pickers;
using PocketTavern.UWP.Controls;
using PocketTavern.UWP.Models;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class ExtensionsPage : Page
    {
        private readonly ExtensionsViewModel _vm = new ExtensionsViewModel();
        private bool _suppressToggles;

        public ExtensionsPage() { this.InitializeComponent(); }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _vm.Load();

            _suppressToggles = true;
            QuickReplyToggle.IsOn    = _vm.QuickReplyEnabled;
            RegexToggle.IsOn         = _vm.RegexEnabled;
            TokenCounterToggle.IsOn  = _vm.TokenCounterEnabled;
            _suppressToggles = false;

            RebuildJsList();
        }

        private void OnBackClick(object sender, RoutedEventArgs e) => App.Navigation.GoBack();

        // ── Native toggles ────────────────────────────────────────────────────

        private void OnQuickReplyToggled(object sender, RoutedEventArgs e)
        {
            if (!_suppressToggles) _vm.QuickReplyEnabled = QuickReplyToggle.IsOn;
        }

        private void OnRegexToggled(object sender, RoutedEventArgs e)
        {
            if (!_suppressToggles) _vm.RegexEnabled = RegexToggle.IsOn;
        }

        private void OnTokenCounterToggled(object sender, RoutedEventArgs e)
        {
            if (!_suppressToggles) _vm.TokenCounterEnabled = TokenCounterToggle.IsOn;
        }

        // ── Native settings navigation ────────────────────────────────────────

        private void OnQuickReplySettingsClick(object sender, RoutedEventArgs e)
        {
            App.Navigation.NavigateToQuickReplySettings();
        }

        private void OnRegexSettingsClick(object sender, RoutedEventArgs e)
        {
            App.Navigation.NavigateToRegexSettings();
        }

        private async void ShowComingSoon(string feature)
        {
            var d = new ContentDialog
            {
                Title = "Coming Soon",
                Content = $"{feature} will be available in a future update.",
                CloseButtonText = "OK"
            };
            await d.ShowAsync();
        }

        // ── JS install ────────────────────────────────────────────────────────

        private async void OnInstallExtensionClick(object sender, RoutedEventArgs e)
        {
            var urlBox = new TextBox
            {
                PlaceholderText = "https://example.com/extension/index.js",
                MinWidth = 260
            };

            var dialog = new ContentDialog
            {
                Title = "Install Extension",
                Content = new SpacedPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "From URL",
                            FontSize = 12, FontWeight = FontWeights.SemiBold,
                            Foreground = (SolidColorBrush)Application.Current.Resources["AccentPrimaryBrush"]
                        },
                        urlBox,
                        new TextBlock
                        {
                            Text = "Or use \"Browse\" to import a .js or .zip file from your device.",
                            FontSize = 12, TextWrapping = TextWrapping.Wrap,
                            Foreground = (SolidColorBrush)Application.Current.Resources["TextSecondaryBrush"]
                        }
                    }
                },
                PrimaryButtonText = "Install URL",
                SecondaryButtonText = "Browse File",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                var url = urlBox.Text.Trim();
                if (string.IsNullOrEmpty(url)) return;
                await _vm.InstallFromUrlAsync(url);
                if (_vm.InstallError != null)
                    await ShowError(_vm.InstallError);
                _vm.ClearInstallError();
                RebuildJsList();
                await App.Extensions.ReloadAsync();
            }
            else if (result == ContentDialogResult.Secondary)
            {
                await PickAndInstallFile();
            }
        }

        private async System.Threading.Tasks.Task PickAndInstallFile()
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.Downloads
            };
            picker.FileTypeFilter.Add(".js");
            picker.FileTypeFilter.Add(".zip");

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            await _vm.InstallFromFileAsync(file);
            if (_vm.InstallError != null)
                await ShowError(_vm.InstallError);
            _vm.ClearInstallError();
            RebuildJsList();
            await App.Extensions.ReloadAsync();
        }

        private async System.Threading.Tasks.Task ShowError(string message)
        {
            var d = new ContentDialog
            {
                Title = "Error",
                Content = message,
                CloseButtonText = "OK"
            };
            await d.ShowAsync();
        }

        // ── JS extension list ─────────────────────────────────────────────────

        private void RebuildJsList()
        {
            JsExtensionsList.Children.Clear();
            var hasAny = _vm.JsExtensions.Count > 0;
            JsEmptyState.Visibility = hasAny ? Visibility.Collapsed : Visibility.Visible;

            foreach (var ext in _vm.JsExtensions)
            {
                var settings = _vm.JsExtensionSettings.TryGetValue(ext.Id, out var s)
                    ? s : new Dictionary<string, object>();
                JsExtensionsList.Children.Add(BuildExtCard(ext, settings));
            }
        }

        private UIElement BuildExtCard(JsExtensionItem ext, Dictionary<string, object> settings)
        {
            var accent     = (SolidColorBrush)Application.Current.Resources["AccentPrimaryBrush"];
            var textPri    = (SolidColorBrush)Application.Current.Resources["TextPrimaryBrush"];
            var textSec    = (SolidColorBrush)Application.Current.Resources["TextSecondaryBrush"];
            var cardBg     = (SolidColorBrush)Application.Current.Resources["BackgroundCardBrush"];
            var surfaceBg  = (SolidColorBrush)Application.Current.Resources["BackgroundSurfaceBrush"];
            var errorBrush = new SolidColorBrush(Color.FromArgb(255, 207, 102, 121));

            var card = new Border
            {
                Margin        = new Thickness(12, 0, 12, 8),
                Padding       = new Thickness(16, 12, 16, 12),
                CornerRadius  = new CornerRadius(10),
                Background    = cardBg
            };

            var stack = new StackPanel();

            // Top row: icon + info + toggle
            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            topRow.Children.Add(new TextBlock
            {
                Text = "\uE74C", FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 22, Foreground = accent, VerticalAlignment = VerticalAlignment.Center
            });

            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            var nameRow = new SpacedPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            nameRow.Children.Add(new TextBlock { Text = ext.Name, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = textPri });
            if (!string.IsNullOrEmpty(ext.Version))
                nameRow.Children.Add(new TextBlock { Text = $"v{ext.Version}", FontSize = 11, Foreground = textSec, VerticalAlignment = VerticalAlignment.Center });
            infoStack.Children.Add(nameRow);
            if (!string.IsNullOrEmpty(ext.Description))
                infoStack.Children.Add(new TextBlock { Text = ext.Description, FontSize = 12, Foreground = textSec, TextWrapping = TextWrapping.Wrap });
            if (!string.IsNullOrEmpty(ext.Author))
                infoStack.Children.Add(new TextBlock { Text = $"by {ext.Author}", FontSize = 11, Foreground = textSec });
            Grid.SetColumn(infoStack, 1);
            topRow.Children.Add(infoStack);

            var toggle = new ToggleSwitch { IsOn = ext.Enabled, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, -8, 0) };
            toggle.Toggled += async (s, e) =>
            {
                _vm.SetJsExtensionEnabled(ext.Id, toggle.IsOn);
                RebuildJsList();
                await App.Extensions.ReloadAsync();
            };
            Grid.SetColumn(toggle, 2);
            topRow.Children.Add(toggle);
            stack.Children.Add(topRow);

            // Settings (if any)
            if (settings.Count > 0)
            {
                stack.Children.Add(new Rectangle { Height = 1, Fill = surfaceBg, Margin = new Thickness(0, 10, 0, 6) });
                var settingsPanel = new SpacedPanel { Spacing = 8 };
                foreach (var kv in settings)
                {
                    var label = CamelCaseToLabel(kv.Key);
                    if (kv.Value is bool boolVal)
                    {
                        var row = new Grid();
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        row.Children.Add(new TextBlock { Text = label, FontSize = 13, Foreground = textPri, VerticalAlignment = VerticalAlignment.Center });
                        var sw = new ToggleSwitch { IsOn = boolVal, Margin = new Thickness(0, 0, -8, 0) };
                        var capturedKey = kv.Key;
                        sw.Toggled += (s, e) => _vm.UpdateJsSetting(ext.Id, capturedKey, sw.IsOn);
                        Grid.SetColumn(sw, 1);
                        row.Children.Add(sw);
                        settingsPanel.Children.Add(row);
                    }
                    else
                    {
                        var tb = new TextBox
                        {
                            Header = label,
                            Text = kv.Value?.ToString() ?? "",
                            Style = (Style)Application.Current.Resources["DarkTextBoxStyle"]
                        };
                        var capturedKey = kv.Key;
                        tb.LostFocus += (s, e) => _vm.UpdateJsSetting(ext.Id, capturedKey, tb.Text);
                        settingsPanel.Children.Add(tb);
                    }
                }
                stack.Children.Add(settingsPanel);
            }

            // Bottom row: source URL + uninstall
            stack.Children.Add(new Rectangle { Height = 1, Fill = surfaceBg, Margin = new Thickness(0, 10, 0, 4) });
            var bottomRow = new Grid();
            bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            if (!string.IsNullOrEmpty(ext.SourceUrl))
            {
                var sourceText = ext.SourceUrl
                    .Replace("https://", "").Replace("http://", "");
                if (sourceText.Length > 40) sourceText = sourceText.Substring(0, 40) + "…";
                bottomRow.Children.Add(new TextBlock
                {
                    Text = sourceText, FontSize = 11, Foreground = textSec,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontFamily = new FontFamily("Consolas")
                });
            }

            if (!ext.Bundled)
            {
                var uninstallBtn = new Button
                {
                    Background = new SolidColorBrush(Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    Tag = ext
                };
                var btnContent = new SpacedPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                btnContent.Children.Add(new TextBlock { Text = "\uE74D", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14, Foreground = errorBrush });
                btnContent.Children.Add(new TextBlock { Text = "Uninstall", FontSize = 13, Foreground = errorBrush });
                uninstallBtn.Content = btnContent;
                uninstallBtn.Click += async (s, e) =>
                {
                    var confirm = new ContentDialog
                    {
                        Title = $"Uninstall {ext.Name}?",
                        Content = "This will permanently delete the extension files.",
                        PrimaryButtonText = "Uninstall",
                        SecondaryButtonText = "Cancel",
                        DefaultButton = ContentDialogButton.Secondary
                    };
                    var result = await confirm.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        _vm.Uninstall(ext.Id);
                        RebuildJsList();
                        await App.Extensions.ReloadAsync();
                    }
                };
                Grid.SetColumn(uninstallBtn, 1);
                bottomRow.Children.Add(uninstallBtn);
            }

            stack.Children.Add(bottomRow);
            card.Child = stack;
            return card;
        }

        private static string CamelCaseToLabel(string key)
        {
            var result = System.Text.RegularExpressions.Regex.Replace(key, "([a-z])([A-Z])", "$1 $2");
            return char.ToUpper(result[0]) + result.Substring(1);
        }
    }
}
