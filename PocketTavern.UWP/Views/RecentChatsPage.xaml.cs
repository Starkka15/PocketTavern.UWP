using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class RecentChatsPage : Page
    {
        private readonly RecentChatsViewModel _vm = new RecentChatsViewModel();
        public RecentChatsPage() { this.InitializeComponent(); }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await _vm.LoadAsync();
            ChatsList.ItemsSource = _vm.Chats;
        }

        private void OnBackClick(object sender, RoutedEventArgs e) => App.Navigation.GoBack();

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await _vm.LoadAsync();
            ChatsList.ItemsSource = null;
            ChatsList.ItemsSource = _vm.Chats;
        }

        private void OnChatClicked(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is RecentChatItem item) _vm.OpenChat(item);
        }

        private async void OnRenameChatClick(object sender, RoutedEventArgs e)
        {
            if (!((sender as Button)?.Tag is RecentChatItem item)) return;

            var box = new TextBox
            {
                Text = item.ChatDisplayName ?? item.FileName,
                PlaceholderText = "Chat name…",
                Style = (Windows.UI.Xaml.Style)Application.Current.Resources["DarkTextBoxStyle"]
            };
            var dialog = new ContentDialog
            {
                Title = "Rename Chat",
                Content = box,
                PrimaryButtonText = "Rename",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                RequestedTheme = Windows.UI.Xaml.ElementTheme.Dark
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await _vm.RenameChatAsync(item, box.Text.Trim());
                ChatsList.ItemsSource = null;
                ChatsList.ItemsSource = _vm.Chats;
            }
        }
    }
}
