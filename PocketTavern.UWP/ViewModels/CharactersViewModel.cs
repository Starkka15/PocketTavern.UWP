using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.ViewModels
{
    /// <summary>Binding wrapper for a character in the list.</summary>
    public class CharacterListItem
    {
        public Character Character { get; }

        public string Name        => Character.Name;
        public string Description => Character.Description;
        public bool   IsFavorite  => Character.IsFavorite;
        public string Avatar      => Character.Avatar;
        public string Initial     => Name?.Length > 0 ? Name[0].ToString().ToUpper() : "?";

        public string AvatarLocalPath { get; }
        public bool   HasAvatar       { get; }

        public CharacterListItem(Character character)
        {
            Character = character;
            var avatarFile = character.Avatar ?? character.Name;
            AvatarLocalPath = App.Characters.GetAvatarPath(avatarFile);
            HasAvatar = File.Exists(AvatarLocalPath);
        }
    }

    public class CharactersViewModel : ViewModelBase
    {
        private ObservableCollection<CharacterListItem> _characters = new ObservableCollection<CharacterListItem>();
        private string _searchQuery = "";
        private bool _isLoading = false;

        public ObservableCollection<CharacterListItem> Characters
        {
            get => _characters;
            set => Set(ref _characters, value);
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set { if (Set(ref _searchQuery, value)) FilterCharacters(); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        private System.Collections.Generic.List<Character> _allCharacters
            = new System.Collections.Generic.List<Character>();

        public async Task LoadAsync()
        {
            IsLoading = true;
            _allCharacters = await App.Characters.GetAllCharactersAsync();
            FilterCharacters();
            IsLoading = false;
        }

        private void FilterCharacters()
        {
            Characters.Clear();
            var q = SearchQuery?.ToLowerInvariant() ?? "";
            foreach (var ch in _allCharacters)
            {
                if (string.IsNullOrEmpty(q) || ch.Name.ToLowerInvariant().Contains(q))
                    Characters.Add(new CharacterListItem(ch));
            }
        }
    }
}
