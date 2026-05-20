using System.Collections.Generic;
using System.Linq;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.Controls;
using PocketTavern.UWP.Models;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class GroupsPage : Page
    {
        private readonly GroupsViewModel _vm = new GroupsViewModel();

        public GroupsPage() { InitializeComponent(); }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await _vm.LoadAsync();
            RebuildList();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }

        // ── New group dialog ─────────────────────────────────────────────────

        private async void NewGroupButton_Click(object sender, RoutedEventArgs e)
        {
            var nameBox = new TextBox
            {
                PlaceholderText = "Group name",
                MinWidth = 260,
                Margin = new Thickness(0, 0, 0, 12)
            };

            var checkPanel = new StackPanel { Spacing = 6 };
            var checkBoxes = new List<CheckBox>();

            foreach (var ch in _vm.AllCharacters)
            {
                var cb = new CheckBox { Content = ch.Name, Tag = ch };
                checkBoxes.Add(cb);
                checkPanel.Children.Add(cb);
            }

            var scrollView = new ScrollViewer
            {
                MaxHeight = 200,
                Content = checkPanel
            };

            var membersLabel = new TextBlock
            {
                Text = "Members",
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Foreground = (SolidColorBrush)Application.Current.Resources["TextSecondaryBrush"],
                Margin = new Thickness(0, 0, 0, 6)
            };

            var content = new StackPanel();
            content.Children.Add(nameBox);
            content.Children.Add(membersLabel);
            content.Children.Add(scrollView);

            var dialog = new ContentDialog
            {
                Title = "New Group",
                Content = content,
                PrimaryButtonText = "Create",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            var selectedAvatars = checkBoxes
                .Where(cb => cb.IsChecked == true)
                .Select(cb => ((Character)cb.Tag).Avatar)
                .ToList();

            var group = _vm.CreateGroup(nameBox.Text, selectedAvatars);
            if (group == null)
            {
                var err = new ContentDialog
                {
                    Title = "Error",
                    Content = _vm.Error,
                    CloseButtonText = "OK"
                };
                _vm.ClearError();
                await err.ShowAsync();
                return;
            }

            RebuildList();
        }

        // ── List ─────────────────────────────────────────────────────────────

        private void RebuildList()
        {
            GroupListPanel.Children.Clear();
            EmptyState.Visibility = _vm.Groups.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;

            var cardBg   = (SolidColorBrush)Application.Current.Resources["BackgroundCardBrush"];
            var textPri  = (SolidColorBrush)Application.Current.Resources["TextPrimaryBrush"];
            var textSec  = (SolidColorBrush)Application.Current.Resources["TextSecondaryBrush"];
            var accent   = (SolidColorBrush)Application.Current.Resources["AccentPrimaryBrush"];
            var errorBrush = new SolidColorBrush(Color.FromArgb(255, 207, 102, 121));

            foreach (var group in _vm.Groups)
            {
                var card = new Border
                {
                    Background = cardBg,
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(16, 12, 16, 12)
                };

                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                infoStack.Children.Add(new TextBlock
                {
                    Text = group.Name,
                    FontSize = 15,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = textPri
                });
                infoStack.Children.Add(new TextBlock
                {
                    Text = _vm.MemberSummary(group),
                    FontSize = 12,
                    Foreground = textSec
                });
                row.Children.Add(infoStack);

                var btnPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 4,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(btnPanel, 1);

                var openBtn = new Button
                {
                    Background = new SolidColorBrush(Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(8, 4, 8, 4),
                    Tag = group
                };
                openBtn.Content = new TextBlock
                {
                    Text = "",
                    FontFamily = new Windows.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
                    FontSize = 16,
                    Foreground = accent
                };
                openBtn.Click += (s, ev) =>
                {
                    var g = (Group)((Button)s).Tag;
                    App.Navigation.NavigateToGroupChat(g.Id);
                };

                var delBtn = new Button
                {
                    Background = new SolidColorBrush(Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(8, 4, 8, 4),
                    Tag = group
                };
                delBtn.Content = new TextBlock
                {
                    Text = "",
                    FontFamily = new Windows.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
                    FontSize = 16,
                    Foreground = errorBrush
                };
                delBtn.Click += async (s, ev) =>
                {
                    var g = (Group)((Button)s).Tag;
                    var confirm = new ContentDialog
                    {
                        Title = $"Delete \"{g.Name}\"?",
                        Content = "This will delete the group and its chat history.",
                        PrimaryButtonText = "Delete",
                        CloseButtonText = "Cancel",
                        DefaultButton = ContentDialogButton.Close
                    };
                    if (await confirm.ShowAsync() == ContentDialogResult.Primary)
                    {
                        _vm.DeleteGroup(g);
                        RebuildList();
                    }
                };

                btnPanel.Children.Add(openBtn);
                btnPanel.Children.Add(delBtn);
                row.Children.Add(btnPanel);

                card.Child = row;
                GroupListPanel.Children.Add(card);
            }
        }
    }
}
