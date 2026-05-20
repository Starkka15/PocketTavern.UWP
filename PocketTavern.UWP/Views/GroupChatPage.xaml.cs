using System;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using System.Collections.Generic;
using System.Linq;
using PocketTavern.UWP.Models;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class GroupChatPage : Page
    {
        private readonly GroupChatViewModel _vm = new GroupChatViewModel();

        public GroupChatPage() { InitializeComponent(); }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string groupId)
                await _vm.LoadGroupAsync(groupId);

            if (_vm.Group != null)
                GroupNameLabel.Text = _vm.Group.Name;

            MessagesList.ItemsSource = _vm.Messages;
            _vm.Messages.CollectionChanged += OnMessagesChanged;
            _vm.PropertyChanged += OnVmPropertyChanged;

            RebuildMemberChips();
            UpdateEmptyState();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _vm.Messages.CollectionChanged -= OnMessagesChanged;
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }

        private async void GenerateFirstMsgButton_Click(object sender, RoutedEventArgs e)
            => await _vm.GenerateFirstMessageAsync();

        private void OnMessagesChanged(object sender,
            System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateEmptyState();
            if (_vm.Messages.Count > 0)
                MessagesList.ScrollIntoView(_vm.Messages[_vm.Messages.Count - 1]);
        }

        private void UpdateEmptyState()
        {
            var empty = _vm.Messages.Count == 0 && !_vm.IsGenerating;
            EmptyStatePanel.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            MessagesList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnVmPropertyChanged(object sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_vm.IsGenerating))
            {
                var gen = _vm.IsGenerating;
                SendButton.IsEnabled = !gen;
                GenerateFirstMsgButton.IsEnabled = !gen;
                StopButton.Visibility = gen ? Visibility.Visible : Visibility.Collapsed;
                SpeakerBar.Visibility = gen ? Visibility.Visible : Visibility.Collapsed;
                UpdateEmptyState();
            }
            if (e.PropertyName == nameof(_vm.CurrentSpeaker))
            {
                SpeakerLabel.Text = string.IsNullOrEmpty(_vm.CurrentSpeaker)
                    ? "" : $"{_vm.CurrentSpeaker} is typing…";
            }
        }

        // ── Member chips ─────────────────────────────────────────────────────

        private void RebuildMemberChips()
        {
            MemberChipsPanel.Children.Clear();
            var accent    = (SolidColorBrush)Application.Current.Resources["AccentPrimaryBrush"];
            var surfaceBg = (SolidColorBrush)Application.Current.Resources["BackgroundSurfaceBrush"];

            foreach (var member in _vm.Members)
            {
                // Avatar image (ms-appdata URI — works for LocalFolder)
                var avatarKey = member.Avatar ?? member.Name;
                var avatarUri = new Uri($"ms-appdata:///local/characters/avatars/{avatarKey}.png");

                var img = new Image
                {
                    Width = 22, Height = 22,
                    Stretch = Stretch.UniformToFill,
                    Source = new BitmapImage(avatarUri)
                };
                var imgClip = new Border
                {
                    Width = 22, Height = 22,
                    CornerRadius = new CornerRadius(11),
                    Margin = new Thickness(0, 0, 5, 0)
                };
                imgClip.Child = img;

                var nameBlock = new TextBlock
                {
                    Text = member.Name,
                    FontSize = 11,
                    Foreground = accent,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(imgClip);
                row.Children.Add(nameBlock);

                var chip = new Border
                {
                    Background = surfaceBg,
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(6, 3, 10, 3),
                    Margin = new Thickness(0, 0, 6, 0)
                };
                chip.Child = row;
                MemberChipsPanel.Children.Add(chip);
            }
        }

        // ── Group settings ───────────────────────────────────────────────────

        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.Group == null) return;
            var g = _vm.Group;
            var sec = (SolidColorBrush)Application.Current.Resources["TextSecondaryBrush"];
            var errorBrush = new SolidColorBrush(Color.FromArgb(255, 207, 102, 121));

            TextBlock SectionLabel(string text) => new TextBlock
            {
                Text = text, FontWeight = FontWeights.SemiBold, FontSize = 12,
                Foreground = sec, Margin = new Thickness(0, 10, 0, 4)
            };

            // System prompt
            var promptBox = new TextBox
            {
                Text = g.SystemPrompt ?? "",
                PlaceholderText = "Group system prompt (optional)…",
                TextWrapping = TextWrapping.Wrap, AcceptsReturn = true,
                Height = 90, Margin = new Thickness(0, 0, 0, 2)
            };

            // Chat style
            var dialogueRb = new RadioButton { Content = "Dialogue", GroupName = "GS_Style",
                IsChecked = g.ChatStyleValue == ChatStyle.Dialogue };
            var rpRb = new RadioButton { Content = "Roleplay (RP)", GroupName = "GS_Style",
                IsChecked = g.ChatStyleValue == ChatStyle.RP, Margin = new Thickness(0, 2, 0, 0) };

            // Activation strategy
            var stratCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            stratCombo.Items.Add(new ComboBoxItem { Content = "Natural",  Tag = ActivationStrategy.Natural });
            stratCombo.Items.Add(new ComboBoxItem { Content = "List (all respond)", Tag = ActivationStrategy.List });
            stratCombo.Items.Add(new ComboBoxItem { Content = "Pooled (random)", Tag = ActivationStrategy.Pooled });
            stratCombo.Items.Add(new ComboBoxItem { Content = "Manual", Tag = ActivationStrategy.Manual });
            stratCombo.SelectedIndex = g.ActivationStrategyValue;

            // Generation mode
            var genCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            genCombo.Items.Add(new ComboBoxItem { Content = "Swap (replace last AI msg)", Tag = GenerationMode.Swap });
            genCombo.Items.Add(new ComboBoxItem { Content = "Append (add new msg)", Tag = GenerationMode.Append });
            genCombo.SelectedIndex = g.GenerationModeValue;

            // Self-responses
            var selfCb = new CheckBox { Content = "Allow characters to respond to themselves",
                IsChecked = g.AllowSelfResponses };

            // Member enable/disable
            var memberPanel = new StackPanel();
            var memberChecks = new Dictionary<string, CheckBox>();
            foreach (var avatar in g.Members)
            {
                var memberChar = _vm.GetMemberByAvatar(avatar);
                var name = memberChar?.Name ?? avatar;
                var cb = new CheckBox
                {
                    Content = name, Tag = avatar,
                    IsChecked = !g.DisabledMembers.Contains(avatar),
                    Margin = new Thickness(0, 2, 0, 0)
                };
                memberChecks[avatar] = cb;
                memberPanel.Children.Add(cb);
            }

            // Delete chat
            var deleteBtn = new Button
            {
                Content = "Delete current chat",
                Background = errorBrush,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 8, 0, 0)
            };
            bool deleteRequested = false;

            var scroll = new ScrollViewer { MaxHeight = 500, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var content = new StackPanel { MinWidth = 300 };
            content.Children.Add(SectionLabel("System Prompt"));
            content.Children.Add(promptBox);
            content.Children.Add(SectionLabel("Chat Style"));
            content.Children.Add(dialogueRb);
            content.Children.Add(rpRb);
            content.Children.Add(SectionLabel("Activation Strategy"));
            content.Children.Add(stratCombo);
            content.Children.Add(SectionLabel("Generation Mode"));
            content.Children.Add(genCombo);
            content.Children.Add(SectionLabel("Members"));
            content.Children.Add(memberPanel);
            content.Children.Add(selfCb);
            content.Children.Add(deleteBtn);
            scroll.Content = content;

            var dialog = new ContentDialog
            {
                Title = "Group Settings", Content = scroll,
                PrimaryButtonText = "Save", CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary, RequestedTheme = ElementTheme.Dark
            };
            deleteBtn.Click += (s, _) => { deleteRequested = true; dialog.Hide(); };

            var result = await dialog.ShowAsync();

            if (deleteRequested)
            {
                var confirm = new ContentDialog
                {
                    Title = "Delete chat?",
                    Content = "This will delete the current chat history. This cannot be undone.",
                    PrimaryButtonText = "Delete", CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close, RequestedTheme = ElementTheme.Dark
                };
                if (await confirm.ShowAsync() == ContentDialogResult.Primary)
                    await _vm.DeleteCurrentChatAsync();
                return;
            }

            if (result != ContentDialogResult.Primary) return;

            var chatStyle       = rpRb.IsChecked == true ? ChatStyle.RP : ChatStyle.Dialogue;
            var stratIdx        = stratCombo.SelectedIndex >= 0 ? stratCombo.SelectedIndex : 0;
            var genModeIdx      = genCombo.SelectedIndex >= 0 ? genCombo.SelectedIndex : 0;
            var allowSelf       = selfCb.IsChecked == true;
            var disabled        = memberChecks.Where(kv => kv.Value.IsChecked != true)
                                             .Select(kv => kv.Key).ToList();
            await _vm.SaveGroupSettingsAsync(promptBox.Text, chatStyle, stratIdx, genModeIdx, allowSelf, disabled);
            RebuildMemberChips();
        }

        // ── Input ────────────────────────────────────────────────────────────

        private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
            => _vm.InputText = InputBox.Text;

        private async void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                await SendAsync();
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
            => await SendAsync();

        private async System.Threading.Tasks.Task SendAsync()
        {
            var text = InputBox.Text;
            InputBox.Text = "";
            _vm.InputText = text;
            await _vm.SendMessageAsync();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
            => _vm.StopGeneration();

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }

        private async void NewChatButton_Click(object sender, RoutedEventArgs e)
        {
            var confirm = new ContentDialog
            {
                Title = "New Chat",
                Content = "Start a new chat? Current history will be kept.",
                PrimaryButtonText = "New Chat",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };
            if (await confirm.ShowAsync() == ContentDialogResult.Primary)
                await _vm.NewChatAsync();
        }
    }
}
