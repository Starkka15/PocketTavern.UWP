using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.ViewModels
{
    public class RecentChatItem
    {
        public string CharacterName { get; set; }
        public string CharacterAvatar { get; set; }
        public string LastMessage { get; set; }
        public int MessageCount { get; set; }
        public string FileName { get; set; }
        public string CharacterInitial => CharacterName?.Length > 0 ? CharacterName[0].ToString().ToUpper() : "?";
        public string DateLabel { get; set; }
        public string AvatarLocalPath { get; set; }
        public bool HasAvatar { get; set; }
    }

    public class RecentChatsViewModel : ViewModelBase
    {
        private ObservableCollection<RecentChatItem> _chats = new ObservableCollection<RecentChatItem>();
        private bool _isLoading = false;

        public ObservableCollection<RecentChatItem> Chats
        {
            get => _chats;
            set => Set(ref _chats, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            Chats.Clear();

            var characters = await App.Characters.GetAllCharactersAsync();
            foreach (var ch in characters)
            {
                var chatInfos = await App.Chats.GetChatInfosAsync(ch.Name);
                if (chatInfos.Count > 0)
                {
                    var latest = chatInfos[0];
                    var avatarFile = ch.Avatar ?? ch.Name;
                    var avatarPath = App.Characters.GetAvatarPath(avatarFile);
                    Chats.Add(new RecentChatItem
                    {
                        CharacterName = ch.Name,
                        CharacterAvatar = ch.Avatar,
                        LastMessage = latest.LastMessage,
                        MessageCount = latest.MessageCount,
                        FileName = latest.FileName,
                        AvatarLocalPath = avatarPath,
                        HasAvatar = File.Exists(avatarPath),
                        DateLabel = DateTimeOffset.FromFileTime(latest.LastModified)
                            .ToLocalTime().ToString("MMM d")
                    });
                }
            }
            IsLoading = false;
        }

        public void OpenChat(RecentChatItem item)
            => App.Navigation.NavigateToChat(item.CharacterAvatar ?? item.CharacterName);
    }
}
