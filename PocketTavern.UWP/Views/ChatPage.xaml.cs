using System;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.Models;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class ChatPage : Page
    {
        private readonly ChatViewModel _vm = new ChatViewModel();

        public ChatPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var avatarUrl = e.Parameter as string ?? "";
            await _vm.InitializeAsync(avatarUrl);

            if (_vm.Character != null)
            {
                CharacterNameLabel.Text = _vm.Character.Name;
                AvatarInitial.Text = _vm.Character.Name.Length > 0
                    ? _vm.Character.Name[0].ToString().ToUpper() : "?";

                // Show avatar image if it exists
                var avatarFile = _vm.Character.Avatar ?? _vm.Character.Name;
                var avatarPath = App.Characters.GetAvatarPath(avatarFile);
                if (System.IO.File.Exists(avatarPath))
                {
                    try
                    {
                        AvatarImage.Source = new Windows.UI.Xaml.Media.Imaging.BitmapImage(
                            new Uri("file:///" + avatarPath.Replace('\\', '/')));
                        AvatarImage.Visibility  = Visibility.Visible;
                        AvatarInitial.Visibility = Visibility.Collapsed;
                    }
                    catch { }
                }
            }

            MessagesList.ItemsSource = _vm.Messages;
            _vm.PropertyChanged += OnVmPropertyChanged;
            RefreshApiIndicator();
            ScrollToBottom();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.Cleanup();
        }

        private void OnVmPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ChatViewModel.IsGenerating))
            {
                SendButton.Visibility = _vm.IsGenerating ? Visibility.Collapsed : Visibility.Visible;
                StopButton.Visibility = _vm.IsGenerating ? Visibility.Visible : Visibility.Collapsed;
            }
            if (e.PropertyName == nameof(ChatViewModel.Messages))
                ScrollToBottom();
            if (e.PropertyName == nameof(ChatViewModel.CurrentApiName) ||
                e.PropertyName == nameof(ChatViewModel.CurrentModelName))
                RefreshApiIndicator();
        }

        private void RefreshApiIndicator()
        {
            var api   = _vm.CurrentApiName ?? "";
            var model = _vm.CurrentModelName ?? "";
            if (string.IsNullOrWhiteSpace(api) && string.IsNullOrWhiteSpace(model))
            {
                ApiIndicatorBar.Visibility = Visibility.Collapsed;
                return;
            }
            ApiNameLabel.Text   = api;
            ModelNameLabel.Text = model;
            ApiIndicatorBar.Visibility = Visibility.Visible;
        }

        private async void OnSendClick(object sender, RoutedEventArgs e)
        {
            _vm.InputText = MessageInput.Text;
            MessageInput.Text = "";
            await _vm.SendMessageAsync();
            ScrollToBottom();
        }

        private async void OnInputKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                _vm.InputText = MessageInput.Text;
                MessageInput.Text = "";
                await _vm.SendMessageAsync();
                ScrollToBottom();
            }
        }

        private async void OnRegenerateClick(object sender, RoutedEventArgs e)
        {
            await _vm.RegenerateAsync();
            ScrollToBottom();
        }

        private async void OnContinueClick(object sender, RoutedEventArgs e)
        {
            await _vm.ContinueAsync();
            ScrollToBottom();
        }

        private void OnStopClick(object sender, RoutedEventArgs e)
            => _vm.StopGeneration();

        private void OnBackClick(object sender, RoutedEventArgs e)
            => App.Navigation.GoBack();

        private void OnCharacterSettingsClick(object sender, RoutedEventArgs e)
        {
            if (_vm.Character != null)
                App.Navigation.NavigateToCharacterSettings(_vm.Character.Avatar ?? _vm.Character.Name);
        }

        private void OnEditCharacterClick(object sender, RoutedEventArgs e)
        {
            if (_vm.Character != null)
                App.Navigation.NavigateToCharacterSettings(_vm.Character.Avatar ?? _vm.Character.Name);
        }

        private async void OnNewChatClick(object sender, RoutedEventArgs e)
        {
            if (_vm.Character == null) return;

            // If character has alternate greetings, show a picker
            var greetings = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(_vm.Character.FirstMessage))
                greetings.Add(_vm.Character.FirstMessage);
            if (_vm.Character.AlternateGreetings != null)
                foreach (var g in _vm.Character.AlternateGreetings)
                    if (!string.IsNullOrEmpty(g)) greetings.Add(g);

            string chosen = greetings.Count > 0 ? greetings[0] : null;

            if (greetings.Count > 1)
            {
                var list = new ListView
                {
                    SelectionMode = ListViewSelectionMode.Single,
                    MaxHeight = 360
                };
                foreach (var g in greetings)
                {
                    list.Items.Add(new TextBlock
                    {
                        Text = g.Length > 80 ? g.Substring(0, 80) + "…" : g,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 4, 0, 4),
                        Style = (Style)Application.Current.Resources["BodyTextStyle"]
                    });
                }
                list.SelectedIndex = 0;

                var dialog = new ContentDialog
                {
                    Title = "Choose Opening Message",
                    Content = list,
                    PrimaryButtonText = "Start",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    RequestedTheme = ElementTheme.Dark
                };

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                    chosen = list.SelectedIndex >= 0 ? greetings[list.SelectedIndex] : chosen;
                else
                    return;
            }

            await _vm.NewChatAsync(chosen);
            ScrollToBottom();
        }

        private async void OnChatHistoryClick(object sender, RoutedEventArgs e)
        {
            if (_vm.Character == null) return;
            var chats = await _vm.GetChatInfosAsync();
            if (chats.Count == 0) return;

            var list = new ListView { SelectionMode = ListViewSelectionMode.Single, MaxHeight = 400 };
            foreach (var c in chats)
            {
                var dt = DateTimeOffset.FromFileTime(c.LastModified).LocalDateTime;
                var preview = string.IsNullOrEmpty(c.LastMessage) ? "(no messages)"
                    : (c.LastMessage.Length > 60 ? c.LastMessage.Substring(0, 60) + "…" : c.LastMessage);
                var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
                panel.Children.Add(new TextBlock
                {
                    Text = dt.ToString("MMM d, yyyy  h:mm tt"),
                    Foreground = (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["TextPrimaryBrush"],
                    FontSize = 13,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold
                });
                panel.Children.Add(new TextBlock
                {
                    Text = preview,
                    Style = (Style)Application.Current.Resources["SubtitleTextStyle"],
                    FontSize = 12
                });
                list.Items.Add(panel);
            }
            list.SelectedIndex = 0;

            var dialog = new ContentDialog
            {
                Title = "Chat History",
                Content = list,
                PrimaryButtonText = "Open",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                RequestedTheme = ElementTheme.Dark
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary && list.SelectedIndex >= 0)
            {
                await _vm.SelectChatAsync(chats[list.SelectedIndex].FileName);
                ScrollToBottom();
            }
        }

        private async void OnDeleteChatClick(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Delete Chat",
                Content = "Delete this conversation? This cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                RequestedTheme = ElementTheme.Dark
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await _vm.DeleteCurrentChatAsync();
                ScrollToBottom();
            }
        }

        private async void OnAuthorsNoteClick(object sender, RoutedEventArgs e)
        {
            var content = App.Settings.GetGlobalAuthorsNoteContent();

            var box = new TextBox
            {
                Text = content,
                PlaceholderText = "Notes injected near the end of the context…",
                AcceptsReturn = true,
                Height = 160,
                Style = (Windows.UI.Xaml.Style)Application.Current.Resources["DarkTextBoxStyle"]
            };

            var dialog = new ContentDialog
            {
                Title = "Author's Note",
                Content = box,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                RequestedTheme = ElementTheme.Dark
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
                App.Settings.SaveGlobalAuthorsNote(box.Text,
                    App.Settings.GetGlobalAuthorsNoteDepth(),
                    App.Settings.GetGlobalAuthorsNoteInterval(),
                    App.Settings.GetGlobalAuthorsNotePosition(),
                    App.Settings.GetGlobalAuthorsNoteRole());
        }

        private async void OnClearChatClick(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Clear Chat",
                Content = "Delete all messages in this chat? This cannot be undone.",
                PrimaryButtonText = "Clear",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                RequestedTheme = ElementTheme.Dark
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await _vm.ClearChatAsync();
                ScrollToBottom();
            }
        }

        private void OnMessageRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            // Walk up the visual tree to find ChatMessage DataContext
            ChatMessage msg = null;
            var el = e.OriginalSource as FrameworkElement;
            while (el != null)
            {
                if (el.DataContext is ChatMessage m) { msg = m; break; }
                el = el.Parent as FrameworkElement;
            }
            if (msg == null) return;
            e.Handled = true;
            ShowMessageMenu(msg, sender as UIElement, e.GetPosition(sender as UIElement));
        }

        private void ShowMessageMenu(ChatMessage msg, UIElement anchor, Windows.Foundation.Point pos)
        {
            var flyout = new MenuFlyout();

            var edit = new MenuFlyoutItem { Text = "Edit" };
            edit.Click += async (s, e) =>
            {
                var box = new TextBox
                {
                    Text = msg.Content,
                    AcceptsReturn = true,
                    Height = 160,
                    Style = (Style)Application.Current.Resources["DarkTextBoxStyle"]
                };
                var dialog = new ContentDialog
                {
                    Title = "Edit Message",
                    Content = box,
                    PrimaryButtonText = "Save",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    RequestedTheme = ElementTheme.Dark
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                    await _vm.EditMessageAsync(msg, box.Text);
            };

            var copy = new MenuFlyoutItem { Text = "Copy" };
            copy.Click += (s, e) =>
            {
                var dp = new DataPackage();
                dp.SetText(msg.Content);
                Clipboard.SetContent(dp);
            };

            var delete = new MenuFlyoutItem { Text = "Delete" };
            delete.Click += async (s, e) => await _vm.DeleteMessageAsync(msg);

            var deleteFrom = new MenuFlyoutItem { Text = "Delete From Here" };
            deleteFrom.Click += async (s, e) => await _vm.DeleteFromHereAsync(msg);

            flyout.Items.Add(edit);
            flyout.Items.Add(copy);
            flyout.Items.Add(new MenuFlyoutSeparator());
            flyout.Items.Add(delete);
            flyout.Items.Add(deleteFrom);

            flyout.ShowAt(anchor, pos);
        }

        private void ScrollToBottom()
        {
            if (MessagesList.Items.Count > 0)
                MessagesList.ScrollIntoView(MessagesList.Items[MessagesList.Items.Count - 1]);
        }
    }
}
