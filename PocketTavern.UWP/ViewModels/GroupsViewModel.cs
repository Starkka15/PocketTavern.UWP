using System.Collections.ObjectModel;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.ViewModels
{
    /// <summary>
    /// Groups are not yet supported in standalone mode.
    /// This is a stub that mirrors Android GroupsViewModel.
    /// </summary>
    public class GroupsViewModel : ViewModelBase
    {
        private bool _showCreateDialog;
        private bool _showDeleteDialog;
        private string _error;
        private string _newGroupName = "";

        public ObservableCollection<Group> Groups { get; } = new ObservableCollection<Group>();

        public bool ShowCreateDialog { get => _showCreateDialog; set => Set(ref _showCreateDialog, value); }
        public bool ShowDeleteDialog { get => _showDeleteDialog; set => Set(ref _showDeleteDialog, value); }
        public string Error           { get => _error;           set => Set(ref _error,           value); }
        public string NewGroupName    { get => _newGroupName;    set => Set(ref _newGroupName,    value); }

        public void Load()
        {
            // Groups are not yet supported
        }

        public void ShowCreateDialogMethod()
        {
            Error = "Group chats are not yet supported.";
        }

        public void DismissCreateDialog()
        {
            ShowCreateDialog = false;
            NewGroupName = "";
        }

        public void CreateGroup()
        {
            Error = "Group chats are not yet supported.";
            DismissCreateDialog();
        }

        public void ShowDeleteConfirmation(Group group)
        {
            ShowDeleteDialog = true;
        }

        public void DismissDeleteDialog() => ShowDeleteDialog = false;

        public void DeleteGroup()
        {
            ShowDeleteDialog = false;
        }

        public void ClearError() => Error = null;
    }
}
