using System;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.Models;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class ConnectionProfilesPage : Page
    {
        private readonly ConnectionProfilesViewModel _vm = new ConnectionProfilesViewModel();

        public ConnectionProfilesPage() { this.InitializeComponent(); }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await _vm.LoadAsync();
            RebuildList();
        }

        private void RebuildList()
        {
            ProfilesList.Items.Clear();
            bool empty = _vm.Profiles.Count == 0;
            EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            ProfilesList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;

            foreach (var profile in _vm.Profiles)
                ProfilesList.Items.Add(BuildCard(profile));
        }

        private UIElement BuildCard(ConnectionProfile profile)
        {
            bool isActive = profile.Id == _vm.ActiveProfileId;

            var accent = (SolidColorBrush)Application.Current.Resources["AccentPrimaryBrush"];
            var textPrimary = (SolidColorBrush)Application.Current.Resources["TextPrimaryBrush"];
            var textSecondary = (SolidColorBrush)Application.Current.Resources["TextSecondaryBrush"];
            var surface = (SolidColorBrush)Application.Current.Resources["BackgroundSurfaceBrush"];
            var errorBrush = new SolidColorBrush(Color.FromArgb(255, 207, 102, 121));

            // Card border
            var card = new Border
            {
                Margin = new Thickness(12, 4, 12, 4),
                Padding = new Thickness(14, 12, 10, 12),
                CornerRadius = new CornerRadius(8),
                Background = surface,
                BorderThickness = new Thickness(isActive ? 1.5 : 0),
                BorderBrush = isActive ? accent : null
            };

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left: text info
            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            // Name row with optional ACTIVE badge
            var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            nameRow.Children.Add(new TextBlock
            {
                Text = profile.Name,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = textPrimary,
                VerticalAlignment = VerticalAlignment.Center
            });

            if (isActive)
            {
                var badge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(40, accent.Color.R, accent.Color.G, accent.Color.B)),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 2, 6, 2),
                    VerticalAlignment = VerticalAlignment.Center
                };
                badge.Child = new TextBlock
                {
                    Text = "ACTIVE",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = accent
                };
                nameRow.Children.Add(badge);
            }
            textStack.Children.Add(nameRow);

            // API type
            textStack.Children.Add(new TextBlock
            {
                Text = profile.ApiLabel,
                FontSize = 13,
                Foreground = isActive ? accent : textSecondary,
                Margin = new Thickness(0, 2, 0, 0)
            });

            // Server URL
            var server = !string.IsNullOrEmpty(profile.CustomUrl) ? profile.CustomUrl : profile.ApiServer;
            if (!string.IsNullOrEmpty(server))
            {
                textStack.Children.Add(new TextBlock
                {
                    Text = server,
                    FontSize = 12,
                    Foreground = textSecondary,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
            }

            // Model
            if (!string.IsNullOrEmpty(profile.Model))
            {
                textStack.Children.Add(new TextBlock
                {
                    Text = profile.Model,
                    FontSize = 12,
                    Foreground = textSecondary,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
            }

            Grid.SetColumn(textStack, 0);
            row.Children.Add(textStack);

            // Right: Activate + Delete buttons
            var btnStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 4
            };

            if (isActive)
            {
                // Checkmark icon instead of button
                btnStack.Children.Add(new TextBlock
                {
                    Text = "\uE73E",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 24,
                    Foreground = accent,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 4, 0)
                });
            }
            else
            {
                var activateBtn = new Button
                {
                    Content = "Activate",
                    FontSize = 13,
                    Padding = new Thickness(12, 6, 12, 6),
                    Background = accent,
                    Foreground = new SolidColorBrush(Colors.Black),
                    BorderThickness = new Thickness(0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Tag = profile
                };
                activateBtn.Click += OnActivateClick;
                btnStack.Children.Add(activateBtn);
            }

            var deleteBtn = new Button
            {
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8),
                VerticalAlignment = VerticalAlignment.Center,
                Tag = profile
            };
            deleteBtn.Content = new TextBlock
            {
                Text = "\uE74D",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 18,
                Foreground = errorBrush
            };
            deleteBtn.Click += OnDeleteClick;
            btnStack.Children.Add(deleteBtn);

            Grid.SetColumn(btnStack, 1);
            row.Children.Add(btnStack);

            card.Child = row;
            return card;
        }

        private void OnBackClick(object sender, RoutedEventArgs e) => App.Navigation.GoBack();

        private async void OnSaveCurrentClick(object sender, RoutedEventArgs e)
        {
            var nameBox = new TextBox { PlaceholderText = "Profile name", MinWidth = 240 };
            var dialog = new ContentDialog
            {
                Title = "Save Current Settings",
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Snapshots your current API config, server, model, and preset selections.",
                            TextWrapping = TextWrapping.Wrap,
                            FontSize = 13,
                            Foreground = (SolidColorBrush)Application.Current.Resources["TextSecondaryBrush"]
                        },
                        nameBox
                    }
                },
                PrimaryButtonText = "Save",
                SecondaryButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            var name = nameBox.Text.Trim();
            if (string.IsNullOrEmpty(name)) return;

            await _vm.SaveCurrentAsync(name);
            RebuildList();
        }

        private async void OnActivateClick(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is ConnectionProfile profile)
            {
                await _vm.ActivateAsync(profile);
                RebuildList();
            }
        }

        private async void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is ConnectionProfile profile)
            {
                var confirm = new ContentDialog
                {
                    Title = "Delete Profile",
                    Content = $"Delete \"{profile.Name}\"?",
                    PrimaryButtonText = "Delete",
                    SecondaryButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Secondary
                };
                var result = await confirm.ShowAsync();
                if (result != ContentDialogResult.Primary) return;

                await _vm.DeleteAsync(profile);
                RebuildList();
            }
        }
    }
}
