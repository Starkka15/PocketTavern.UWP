using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using PocketTavern.UWP.Controls;
using PocketTavern.UWP.Models;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class QuickReplySettingsPage : Page
    {
        private readonly QuickReplySettingsViewModel _vm = new QuickReplySettingsViewModel();

        // Brush helpers
        private SolidColorBrush Accent     => (SolidColorBrush)Application.Current.Resources["AccentPrimaryBrush"];
        private SolidColorBrush TextPri    => (SolidColorBrush)Application.Current.Resources["TextPrimaryBrush"];
        private SolidColorBrush TextSec    => (SolidColorBrush)Application.Current.Resources["TextSecondaryBrush"];
        private SolidColorBrush CardBg     => (SolidColorBrush)Application.Current.Resources["BackgroundCardBrush"];
        private SolidColorBrush SurfaceBg  => (SolidColorBrush)Application.Current.Resources["BackgroundSurfaceBrush"];
        private static SolidColorBrush ErrorBrush => new SolidColorBrush(Color.FromArgb(255, 207, 102, 121));

        public QuickReplySettingsPage() { this.InitializeComponent(); }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _vm.Load();
            Rebuild();
        }

        private void OnBackClick(object sender, RoutedEventArgs e) => App.Navigation.GoBack();

        private async void OnAddPresetClick(object sender, RoutedEventArgs e)
        {
            var nameBox = new TextBox { PlaceholderText = "Preset name", MinWidth = 260 };
            var dialog = new ContentDialog
            {
                Title = "Add Preset",
                Content = nameBox,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var name = nameBox.Text.Trim();
                if (string.IsNullOrEmpty(name)) return;
                _vm.AddPreset(name);
                Rebuild();
            }
        }

        // ── Rebuild ──────────────────────────────────────────────────────────

        private void Rebuild()
        {
            RootPanel.Children.Clear();

            if (_vm.Presets.Count == 0)
            {
                RootPanel.Children.Add(BuildEmptyState());
                return;
            }

            foreach (var preset in _vm.Presets)
                RootPanel.Children.Add(BuildPresetCard(preset));
        }

        // ── Empty state ──────────────────────────────────────────────────────

        private UIElement BuildEmptyState()
        {
            var panel = new SpacedPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(32, 48, 32, 0),
                Spacing = 0
            };
            panel.Children.Add(new TextBlock
            {
                Text = "\uE8BD",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 48,
                Foreground = TextSec,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            panel.Children.Add(new TextBlock
            {
                Text = "No presets yet",
                FontSize = 16,
                Foreground = TextSec,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 4)
            });
            panel.Children.Add(new TextBlock
            {
                Text = "Add your first preset",
                FontSize = 13,
                Foreground = TextSec,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            });

            var addBtn = new Button
            {
                Content = "Add Preset",
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Accent,
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(1),
                BorderBrush = Accent,
                Padding = new Thickness(20, 8, 20, 8)
            };
            addBtn.Click += OnAddPresetClick;
            panel.Children.Add(addBtn);
            return panel;
        }

        // ── Preset card ──────────────────────────────────────────────────────

        private UIElement BuildPresetCard(QuickReplyPreset preset)
        {
            var card = new Border
            {
                Margin = new Thickness(12, 0, 12, 8),
                Padding = new Thickness(16, 12, 16, 12),
                CornerRadius = new CornerRadius(10),
                Background = CardBg
            };

            var stack = new StackPanel();

            // Top row: name + count + edit + delete + toggle
            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Name + count stacked
            var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            nameStack.Children.Add(new TextBlock
            {
                Text = preset.Name,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextPri
            });
            var btnCount = preset.Buttons.Count;
            nameStack.Children.Add(new TextBlock
            {
                Text = btnCount == 1 ? "1 button" : $"{btnCount} buttons",
                FontSize = 12,
                Foreground = TextSec
            });
            Grid.SetColumn(nameStack, 0);
            topRow.Children.Add(nameStack);

            // Edit pencil
            var editBtn = MakeIconButton("\uE70F", Accent);
            editBtn.Margin = new Thickness(4, 0, 0, 0);
            editBtn.Click += async (s, e) =>
            {
                var nameBox = new TextBox { Text = preset.Name, MinWidth = 260 };
                var d = new ContentDialog
                {
                    Title = "Rename Preset",
                    Content = nameBox,
                    PrimaryButtonText = "Save",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary
                };
                var res = await d.ShowAsync();
                if (res == ContentDialogResult.Primary)
                {
                    var n = nameBox.Text.Trim();
                    if (!string.IsNullOrEmpty(n)) { _vm.RenamePreset(preset.Id, n); Rebuild(); }
                }
            };
            Grid.SetColumn(editBtn, 1);
            topRow.Children.Add(editBtn);

            // Delete trash
            var delBtn = MakeIconButton("\uE74D", ErrorBrush);
            delBtn.Margin = new Thickness(4, 0, 0, 0);
            delBtn.Click += async (s, e) =>
            {
                var confirm = new ContentDialog
                {
                    Title = $"Delete \"{preset.Name}\"?",
                    Content = "This will permanently delete the preset and all its buttons.",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close
                };
                var res = await confirm.ShowAsync();
                if (res == ContentDialogResult.Primary) { _vm.DeletePreset(preset.Id); Rebuild(); }
            };
            Grid.SetColumn(delBtn, 2);
            topRow.Children.Add(delBtn);

            // Toggle switch
            var toggle = new ToggleSwitch
            {
                IsOn = preset.Enabled,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, -8, 0)
            };
            toggle.Toggled += (s, e) =>
            {
                if (toggle.IsOn != preset.Enabled)
                {
                    _vm.TogglePreset(preset.Id);
                    // don't rebuild, toggle is already correct
                }
            };
            Grid.SetColumn(toggle, 4);
            topRow.Children.Add(toggle);

            stack.Children.Add(topRow);

            // Button rows
            if (preset.Buttons.Count > 0)
            {
                stack.Children.Add(new Rectangle
                {
                    Height = 1,
                    Fill = SurfaceBg,
                    Margin = new Thickness(0, 10, 0, 6)
                });

                foreach (var btn in preset.Buttons)
                    stack.Children.Add(BuildButtonRow(preset, btn));
            }

            // Add button link
            stack.Children.Add(new Rectangle
            {
                Height = 1,
                Fill = SurfaceBg,
                Margin = new Thickness(0, 8, 0, 4)
            });

            var addBtnRow = new Button
            {
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var addContent = new SpacedPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            addContent.Children.Add(new TextBlock
            {
                Text = "\uE710",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 13,
                Foreground = Accent,
                VerticalAlignment = VerticalAlignment.Center
            });
            addContent.Children.Add(new TextBlock { Text = "Add Button", FontSize = 13, Foreground = Accent });
            addBtnRow.Content = addContent;
            addBtnRow.Click += async (s, e) =>
            {
                var dlg = await ShowButtonDialog(null, null, new HashSet<string>());
                if (dlg != null) { _vm.AddButton(preset.Id, dlg.Label, dlg.Message, dlg.Triggers); Rebuild(); }
            };
            stack.Children.Add(addBtnRow);

            card.Child = stack;
            return card;
        }

        // ── Button row ───────────────────────────────────────────────────────

        private UIElement BuildButtonRow(QuickReplyPreset preset, QuickReplyButton btn)
        {
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            infoStack.Children.Add(new TextBlock
            {
                Text = btn.Label,
                FontSize = 13,
                Foreground = Accent
            });

            var preview = btn.Message.Length > 50 ? btn.Message.Substring(0, 50) + "…" : btn.Message;
            infoStack.Children.Add(new TextBlock
            {
                Text = preview,
                FontSize = 12,
                Foreground = TextSec,
                TextWrapping = TextWrapping.NoWrap
            });
            Grid.SetColumn(infoStack, 0);
            row.Children.Add(infoStack);

            // Edit
            var editBtn = MakeIconButton("\uE70F", Accent);
            editBtn.Click += async (s, e) =>
            {
                var dlg = await ShowButtonDialog(btn.Label, btn.Message, btn.AutoTriggers);
                if (dlg != null) { _vm.UpdateButton(preset.Id, btn.Id, dlg.Label, dlg.Message, dlg.Triggers); Rebuild(); }
            };
            Grid.SetColumn(editBtn, 1);
            row.Children.Add(editBtn);

            // Delete
            var delBtn = MakeIconButton("\uE74D", ErrorBrush);
            delBtn.Click += async (s, e) =>
            {
                var confirm = new ContentDialog
                {
                    Title = $"Delete button \"{btn.Label}\"?",
                    Content = "This will permanently delete this button.",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close
                };
                var res = await confirm.ShowAsync();
                if (res == ContentDialogResult.Primary) { _vm.DeleteButton(preset.Id, btn.Id); Rebuild(); }
            };
            Grid.SetColumn(delBtn, 2);
            row.Children.Add(delBtn);

            return row;
        }

        // ── Add/Edit Button dialog ───────────────────────────────────────────

        private class ButtonDialogResult
        {
            public string Label, Message;
            public HashSet<string> Triggers;
        }

        private async System.Threading.Tasks.Task<ButtonDialogResult>
            ShowButtonDialog(string existingLabel, string existingMessage, HashSet<string> existingTriggers)
        {
            var labelBox = new TextBox
            {
                Header = "Label",
                PlaceholderText = "Button label (required)",
                Text = existingLabel ?? ""
            };
            var messageBox = new TextBox
            {
                Header = "Message",
                PlaceholderText = "Message to send",
                Text = existingMessage ?? "",
                AcceptsReturn = true,
                Height = 80,
                TextWrapping = TextWrapping.Wrap
            };

            var chkChatLoad = new CheckBox
            {
                Content = "On chat load",
                IsChecked = existingTriggers?.Contains("CHAT_CHANGED") == true
            };
            var chkAfterAi = new CheckBox
            {
                Content = "After AI message",
                IsChecked = existingTriggers?.Contains("MESSAGE_RECEIVED") == true
            };
            var chkAfterUser = new CheckBox
            {
                Content = "After user message",
                IsChecked = existingTriggers?.Contains("MESSAGE_SENT") == true
            };
            var chkAfterGen = new CheckBox
            {
                Content = "After generation",
                IsChecked = existingTriggers?.Contains("GENERATION_STOPPED") == true
            };

            var content = new SpacedPanel { Spacing = 10, MinWidth = 280 };
            content.Children.Add(labelBox);
            content.Children.Add(messageBox);
            content.Children.Add(new TextBlock
            {
                Text = "Auto-trigger",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = (SolidColorBrush)Application.Current.Resources["TextPrimaryBrush"],
                Margin = new Thickness(0, 4, 0, 0)
            });
            content.Children.Add(chkChatLoad);
            content.Children.Add(chkAfterAi);
            content.Children.Add(chkAfterUser);
            content.Children.Add(chkAfterGen);

            var dialog = new ContentDialog
            {
                Title = existingLabel == null ? "Add Button" : "Edit Button",
                Content = content,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return null;

            var label = labelBox.Text.Trim();
            if (string.IsNullOrEmpty(label)) return null;

            var triggers = new HashSet<string>();
            if (chkChatLoad.IsChecked == true)   triggers.Add("CHAT_CHANGED");
            if (chkAfterAi.IsChecked == true)    triggers.Add("MESSAGE_RECEIVED");
            if (chkAfterUser.IsChecked == true)  triggers.Add("MESSAGE_SENT");
            if (chkAfterGen.IsChecked == true)   triggers.Add("GENERATION_STOPPED");

            return new ButtonDialogResult { Label = label, Message = messageBox.Text, Triggers = triggers };
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static Button MakeIconButton(string glyph, SolidColorBrush brush)
        {
            return new Button
            {
                Content = new TextBlock
                {
                    Text = glyph,
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 16,
                    Foreground = brush
                },
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4),
                VerticalAlignment = VerticalAlignment.Center
            };
        }
    }
}
