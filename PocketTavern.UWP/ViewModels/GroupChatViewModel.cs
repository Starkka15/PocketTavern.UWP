using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.ViewModels
{
    /// <summary>
    /// Group chat is not yet supported in standalone mode.
    /// Stub that mirrors Android GroupChatViewModel.
    /// </summary>
    public class GroupChatViewModel : ViewModelBase
    {
        private Group _group;
        private string _inputText = "";
        private string _error;

        public Group Group         { get => _group;     set => Set(ref _group,     value); }
        public string InputText    { get => _inputText; set => Set(ref _inputText, value); }
        public string Error        { get => _error;     set => Set(ref _error,     value); }

        public void LoadGroup(string groupId)
        {
            Error = "Group chats are not yet supported.";
        }

        public void SendMessage()
        {
            Error = "Group chats are not yet supported.";
        }

        public void ClearError() => Error = null;
    }
}
