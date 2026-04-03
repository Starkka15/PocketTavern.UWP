using System;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Newtonsoft.Json.Linq;
using PocketTavern.UWP.Models;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class ChatPage : Page
    {
        private readonly ChatViewModel _vm = new ChatViewModel();
        private readonly DispatcherTimer _typingTimer = new DispatcherTimer();
        private int _typingStep = 0;

        public ChatPage()
        {
            this.InitializeComponent();
            _typingTimer.Interval = TimeSpan.FromMilliseconds(400);
            _typingTimer.Tick += OnTypingTick;
        }

        private void OnTypingTick(object sender, object e)
        {
            _typingStep = (_typingStep + 1) % 3;
            TypingDot1.Opacity = _typingStep == 0 ? 1.0 : 0.3;
            TypingDot2.Opacity = _typingStep == 1 ? 1.0 : 0.3;
            TypingDot3.Opacity = _typingStep == 2 ? 1.0 : 0.3;
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
            InputPane.GetForCurrentView().Showing += OnKeyboardShowing;
            RefreshApiIndicator();
            RebuildQuickReplyBar();
            RefreshBackground();
            ScrollToBottom();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _vm.PropertyChanged -= OnVmPropertyChanged;
            InputPane.GetForCurrentView().Showing -= OnKeyboardShowing;
            _vm.Cleanup();
            _typingTimer.Stop();
        }

        private void OnVmPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ChatViewModel.IsGenerating))
            {
                SendButton.Visibility = _vm.IsGenerating ? Visibility.Collapsed : Visibility.Visible;
                StopButton.Visibility = _vm.IsGenerating ? Visibility.Visible : Visibility.Collapsed;
                QuickReplyBar.Visibility = _vm.IsGenerating || _vm.QuickReplyButtons.Count == 0
                    ? Visibility.Collapsed : Visibility.Visible;
                RefreshTypingIndicator();
            }
            if (e.PropertyName == nameof(ChatViewModel.CurrentStreamingText))
            {
                RefreshTypingIndicator();
                ScrollToBottom();
            }
            if (e.PropertyName == nameof(ChatViewModel.Messages))
                ScrollToBottom();
            if (e.PropertyName == nameof(ChatViewModel.CurrentApiName) ||
                e.PropertyName == nameof(ChatViewModel.CurrentModelName))
                RefreshApiIndicator();
            if (e.PropertyName == nameof(ChatViewModel.QuickReplyButtons))
                RebuildQuickReplyBar();
            if (e.PropertyName == nameof(ChatViewModel.BackgroundPath))
                RefreshBackground();
        }

        private void RefreshTypingIndicator()
        {
            // Show dots only while generating and before first token arrives
            var showDots = _vm.IsGenerating && string.IsNullOrEmpty(_vm.CurrentStreamingText);
            TypingIndicator.Visibility = showDots ? Visibility.Visible : Visibility.Collapsed;
            if (showDots) _typingTimer.Start();
            else          _typingTimer.Stop();
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

            // Notify extensions of long-press (fires MESSAGE_LONG_PRESSED event in JS)
            var msgIdx = _vm.GetMessageIndex(msg);
            _vm.ShowMessageActions(msgIdx);

            ShowMessageMenu(msg, msgIdx, sender as UIElement, e.GetPosition(sender as UIElement));
        }

        private void ShowMessageMenu(ChatMessage msg, int msgIdx, UIElement anchor, Windows.Foundation.Point pos)
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

            if (!msg.IsUser && msg.SwipeCount > 1)
            {
                flyout.Items.Add(new MenuFlyoutSeparator());
                if (msg.HasPrevSwipe)
                {
                    var swipeL = new MenuFlyoutItem { Text = $"\uE76B  Previous ({msg.CurrentSwipeIndex}/{msg.SwipeCount})" };
                    swipeL.Click += async (s, e) => { await _vm.SwipeLeftAsync(msg); ScrollToBottom(); };
                    flyout.Items.Add(swipeL);
                }
                if (msg.HasNextSwipe)
                {
                    var swipeR = new MenuFlyoutItem { Text = $"\uE76C  Next ({msg.CurrentSwipeIndex + 2}/{msg.SwipeCount})" };
                    swipeR.Click += async (s, e) => { await _vm.SwipeRightAsync(msg); ScrollToBottom(); };
                    flyout.Items.Add(swipeR);
                }
            }

            var ttsConfig = App.Settings.GetTtsConfig();
            if (ttsConfig.Enabled)
            {
                flyout.Items.Add(new MenuFlyoutSeparator());
                if (_vm.IsTtsSpeaking)
                {
                    var stopTts = new MenuFlyoutItem { Text = "Stop TTS", Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xFF, 0x74, 0x3C)) };
                    stopTts.Click += (s, e) => _vm.StopTts();
                    flyout.Items.Add(stopTts);
                }
                else
                {
                    var speakTts = new MenuFlyoutItem { Text = "Play TTS" };
                    speakTts.Click += async (s, e) => await _vm.SpeakMessageAsync(msg);
                    flyout.Items.Add(speakTts);
                }
            }

            // Extension message actions
            var extActions = App.Extensions.GetMessageActionSets();
            if (extActions.Count > 0)
            {
                flyout.Items.Add(new MenuFlyoutSeparator());
                foreach (var extKv in extActions)
                {
                    foreach (var actionObj in extKv.Value)
                    {
                        var jAction = actionObj as Newtonsoft.Json.Linq.JObject;
                        if (jAction == null) continue;
                        var label  = jAction.Value<string>("label") ?? "";
                        var action = jAction.Value<string>("action") ?? "";
                        if (string.IsNullOrEmpty(label)) continue;
                        var item = new MenuFlyoutItem { Text = label };
                        var capturedAction = action;
                        var capturedLabel  = label;
                        item.Click += async (s, e) =>
                        {
                            var safeAction = _vm.EscapeJsonString(capturedAction);
                            var safeLabel  = _vm.EscapeJsonString(capturedLabel);
                            await App.Extensions.DispatchEventAsync("BUTTON_CLICKED",
                                $"{{\"action\":\"{safeAction}\",\"label\":\"{safeLabel}\"}}");
                        };
                        flyout.Items.Add(item);
                    }
                }
            }

            flyout.ShowAt(anchor, pos);
        }

        // ── Background / Gallery / Delete Character handlers ─────────────────

        private void RefreshBackground()
        {
            if (!string.IsNullOrEmpty(_vm.BackgroundPath))
            {
                try
                {
                    ChatBackground.Source = new Windows.UI.Xaml.Media.Imaging.BitmapImage(
                        new Uri("file:///" + _vm.BackgroundPath.Replace('\\', '/')));
                    ChatBackground.Visibility = Visibility.Visible;
                    ClearBackgroundItem.Visibility = Visibility.Visible;
                }
                catch
                {
                    ChatBackground.Visibility = Visibility.Collapsed;
                    ClearBackgroundItem.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                ChatBackground.Source = null;
                ChatBackground.Visibility = Visibility.Collapsed;
                ClearBackgroundItem.Visibility = Visibility.Collapsed;
            }
        }

        private async void OnUploadBackgroundClick(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".bmp");
            picker.FileTypeFilter.Add(".webp");
            var file = await picker.PickSingleFileAsync();
            if (file != null)
                await _vm.UploadBackgroundAsync(file);
        }

        private async void OnClearBackgroundClick(object sender, RoutedEventArgs e)
        {
            _vm.ClearBackground();
        }

        private async void OnImageGalleryClick(object sender, RoutedEventArgs e)
        {
            var images = await _vm.GetGalleryImagesAsync();

            var panel = new StackPanel();

            if (images.Count == 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "No images found in this character's chats.",
                    Style = (Style)Application.Current.Resources["SubtitleTextStyle"]
                });
            }
            else
            {
                var grid = new GridView
                {
                    ItemsPanel = (ItemsPanelTemplate)Windows.UI.Xaml.Markup.XamlReader.Load(
                        @"<ItemsPanelTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
                            <ItemsWrapPanel Orientation='Horizontal'/>
                        </ItemsPanelTemplate>"),
                    MaxHeight = 500
                };
                foreach (var img in images)
                {
                    try
                    {
                        var bmp = new Windows.UI.Xaml.Media.Imaging.BitmapImage(
                            new Uri("file:///" + img.ImagePath.Replace('\\', '/')));
                        var imgCtrl = new Image { Source = bmp, Width = 120, Height = 120, Stretch = Windows.UI.Xaml.Media.Stretch.UniformToFill, Margin = new Thickness(4) };
                        grid.Items.Add(imgCtrl);
                    }
                    catch { }
                }
                panel.Children.Add(grid);
            }

            var dialog = new ContentDialog
            {
                Title = "Image Gallery",
                Content = new ScrollViewer { Content = panel },
                CloseButtonText = "Close",
                RequestedTheme = ElementTheme.Dark
            };
            await dialog.ShowAsync();
        }

        private async void OnDeleteCharacterClick(object sender, RoutedEventArgs e)
        {
            var name = _vm.Character?.Name ?? "this character";
            var dialog = new ContentDialog
            {
                Title = "Delete Character",
                Content = $"Delete \"{name}\" and all their chats? This cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                RequestedTheme = ElementTheme.Dark
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await _vm.DeleteCharacterAsync();
                App.Navigation.GoBack();
            }
        }

        private void OnDebugLogClick(object sender, RoutedEventArgs e)
            => App.Navigation.NavigateToDebugLog();

        // ── Quick Reply bar ───────────────────────────────────────────────────

        private void RebuildQuickReplyBar()
        {
            QuickReplyPanel.Children.Clear();

            var buttons = _vm.QuickReplyButtons;
            if (buttons.Count == 0 || _vm.IsGenerating)
            {
                QuickReplyBar.Visibility = Visibility.Collapsed;
                return;
            }

            var surface = (SolidColorBrush)Application.Current.Resources["BackgroundCardBrush"];
            var textPri = (SolidColorBrush)Application.Current.Resources["TextPrimaryBrush"];
            var accent  = (SolidColorBrush)Application.Current.Resources["AccentPrimaryBrush"];

            foreach (var btn in buttons)
            {
                var button = new Button
                {
                    Content         = btn.Label,
                    Padding         = new Thickness(12, 5, 12, 5),
                    Background      = surface,
                    Foreground      = textPri,
                    BorderBrush     = accent,
                    BorderThickness = new Thickness(1),
                    FontSize        = 13,
                    Margin          = new Thickness(0)
                };

                var captured = btn;
                button.Click += async (s, e) =>
                {
                    await _vm.SendQuickReplyAsync(captured);
                    ScrollToBottom();
                };

                QuickReplyPanel.Children.Add(button);
            }

            // Show bar; respect user's collapsed preference
            QuickReplyBar.Visibility = Visibility.Visible;
            var expanded = App.Settings.GetQuickReplyBarVisible();
            QuickReplyScroll.Visibility  = expanded ? Visibility.Visible : Visibility.Collapsed;
            QuickReplyChevron.Text = expanded ? "\uE70E" : "\uE70D"; // chevron up / chevron down
        }

        private void OnQuickReplyToggleClick(object sender, RoutedEventArgs e)
        {
            var nowExpanded = QuickReplyScroll.Visibility == Visibility.Collapsed;
            App.Settings.SetQuickReplyBarVisible(nowExpanded);
            QuickReplyScroll.Visibility  = nowExpanded ? Visibility.Visible : Visibility.Collapsed;
            QuickReplyChevron.Text = nowExpanded ? "\uE70E" : "\uE70D";
        }

        // ── Scroll ────────────────────────────────────────────────────────────

        private void OnKeyboardShowing(InputPane sender, InputPaneVisibilityEventArgs args)
            => ScrollToBottom();

        private void OnInputGotFocus(object sender, RoutedEventArgs e)
            => ScrollToBottom();

        private void ScrollToBottom()
        {
            // Defer to Low priority so the ListView has finished its layout pass first
            var _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, () =>
            {
                if (MessagesList.Items.Count > 0)
                    MessagesList.ScrollIntoView(MessagesList.Items[MessagesList.Items.Count - 1]);
            });
        }
    }
}
