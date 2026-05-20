using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.ViewModels
{
    public class GroupsViewModel : ViewModelBase
    {
        public ObservableCollection<Group> Groups { get; } = new ObservableCollection<Group>();
        public ObservableCollection<Character> AllCharacters { get; } = new ObservableCollection<Character>();

        private string _error;
        public string Error { get => _error; set => Set(ref _error, value); }

        public async Task LoadAsync()
        {
            var groups = await App.Groups.GetAllGroupsAsync();
            Groups.Clear();
            foreach (var g in groups) Groups.Add(g);

            var chars = await App.Characters.GetAllCharactersAsync();
            AllCharacters.Clear();
            foreach (var c in chars) AllCharacters.Add(c);
        }

        public Group CreateGroup(string name, List<string> memberAvatars)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                Error = "Group name cannot be empty.";
                return null;
            }
            if (memberAvatars == null || memberAvatars.Count == 0)
            {
                Error = "A group needs at least one member.";
                return null;
            }
            var group = App.Groups.CreateGroup(name.Trim(), memberAvatars);
            Groups.Add(group);
            return group;
        }

        public void DeleteGroup(Group group)
        {
            App.Groups.DeleteGroup(group.Id);
            Groups.Remove(group);
        }

        public string MemberSummary(Group group)
        {
            if (group.Members == null || group.Members.Count == 0) return "No members";
            return group.Members.Count == 1 ? "1 member" : $"{group.Members.Count} members";
        }

        public void ClearError() => Error = null;
    }
}
