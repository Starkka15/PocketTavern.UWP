using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.ViewModels
{
    public class CreateCharacterViewModel : ViewModelBase
    {
        private string _editingFileName = null; // null = creating new, non-null = editing existing

        private string _name = "";
        private string _description = "";
        private string _personality = "";
        private string _scenario = "";
        private string _firstMessage = "";
        private string _messageExample = "";
        private string _creatorNotes = "";
        private string _systemPrompt = "";
        private string _postHistoryInstructions = "";
        private string _tagsText = "";
        private string _avatarFilePath = "";
        private bool _isSaving = false;
        private bool _hasCharacterBook = false;
        private int _characterBookEntryCount = 0;

        public string Name                    { get => _name; set => Set(ref _name, value); }
        public string Description             { get => _description; set => Set(ref _description, value); }
        public string Personality             { get => _personality; set => Set(ref _personality, value); }
        public string Scenario                { get => _scenario; set => Set(ref _scenario, value); }
        public string FirstMessage            { get => _firstMessage; set => Set(ref _firstMessage, value); }
        public string MessageExample          { get => _messageExample; set => Set(ref _messageExample, value); }
        public string CreatorNotes            { get => _creatorNotes; set => Set(ref _creatorNotes, value); }
        public string SystemPrompt            { get => _systemPrompt; set => Set(ref _systemPrompt, value); }
        public string PostHistoryInstructions { get => _postHistoryInstructions; set => Set(ref _postHistoryInstructions, value); }
        public string TagsText                { get => _tagsText; set => Set(ref _tagsText, value); }
        public string AvatarFilePath          { get => _avatarFilePath; set => Set(ref _avatarFilePath, value); }
        public bool   IsSaving                { get => _isSaving; set => Set(ref _isSaving, value); }
        public bool   HasCharacterBook        { get => _hasCharacterBook; set => Set(ref _hasCharacterBook, value); }
        public int    CharacterBookEntryCount { get => _characterBookEntryCount; set => Set(ref _characterBookEntryCount, value); }

        public ObservableCollection<string> AlternateGreetings { get; } = new ObservableCollection<string>();

        public bool IsEditing => _editingFileName != null;
        public bool CanSave => !string.IsNullOrWhiteSpace(Name);

        public async Task LoadForEditAsync(string fileName)
        {
            _editingFileName = fileName;
            var character = await App.Characters.GetCharacterAsync(fileName);
            if (character == null) return;
            Name                    = character.Name;
            Description             = character.Description;
            Personality             = character.Personality;
            Scenario                = character.Scenario;
            FirstMessage            = character.FirstMessage;
            MessageExample          = character.MessageExample;
            CreatorNotes            = character.CreatorNotes;
            SystemPrompt            = character.SystemPrompt;
            PostHistoryInstructions = character.PostHistoryInstructions;
            TagsText = string.Join(", ", character.Tags ?? new List<string>());
            HasCharacterBook        = character.HasCharacterBook;
            CharacterBookEntryCount = character.CharacterBookEntryCount;
            AlternateGreetings.Clear();
            foreach (var g in character.AlternateGreetings ?? new List<string>())
                AlternateGreetings.Add(g);
        }

        public async Task SaveAsync()
        {
            if (!CanSave) return;
            IsSaving = true;

            if (_editingFileName != null)
                await SaveEditAsync();
            else
                await SaveNewAsync();

            IsSaving = false;
            App.Navigation.GoBack();
        }

        private async Task SaveEditAsync()
        {
            var existing = await App.Characters.GetCharacterAsync(_editingFileName);
            if (existing == null) return;

            var tags = ParseTags();
            existing.Name                    = Name.Trim();
            existing.Description             = Description;
            existing.Personality             = Personality;
            existing.Scenario                = Scenario;
            existing.FirstMessage            = FirstMessage;
            existing.MessageExample          = MessageExample;
            existing.CreatorNotes            = CreatorNotes;
            existing.SystemPrompt            = SystemPrompt;
            existing.PostHistoryInstructions = PostHistoryInstructions;
            existing.Tags                    = tags;
            existing.AlternateGreetings      = new List<string>(AlternateGreetings);

            if (!string.IsNullOrEmpty(AvatarFilePath) && System.IO.File.Exists(AvatarFilePath))
            {
                var ext = System.IO.Path.GetExtension(AvatarFilePath);
                var avatarName = SanitizeFileName(Name.Trim()) + ext;
                await App.Characters.CopyAvatarAsync(AvatarFilePath, avatarName);
                existing.Avatar = avatarName;
            }

            await App.Characters.SaveCharacterAsync(_editingFileName, existing);
        }

        private async Task SaveNewAsync()
        {
            var tags = ParseTags();
            var fileName = SanitizeFileName(Name.Trim());

            string avatarPath = null;
            if (!string.IsNullOrEmpty(AvatarFilePath) && System.IO.File.Exists(AvatarFilePath))
            {
                var ext = System.IO.Path.GetExtension(AvatarFilePath);
                avatarPath = fileName + ext;
                await App.Characters.CopyAvatarAsync(AvatarFilePath, avatarPath);
            }

            var character = new Character
            {
                Name                    = Name.Trim(),
                Description             = Description,
                Personality             = Personality,
                Scenario                = Scenario,
                FirstMessage            = FirstMessage,
                MessageExample          = MessageExample,
                CreatorNotes            = CreatorNotes,
                SystemPrompt            = SystemPrompt,
                PostHistoryInstructions = PostHistoryInstructions,
                Tags                    = tags,
                AlternateGreetings      = new List<string>(AlternateGreetings),
                Avatar                  = avatarPath ?? fileName
            };

            await App.Characters.SaveCharacterAsync(fileName, character);
        }

        private List<string> ParseTags()
        {
            var tags = new List<string>();
            if (!string.IsNullOrEmpty(TagsText))
                foreach (var t in TagsText.Split(','))
                    if (!string.IsNullOrWhiteSpace(t)) tags.Add(t.Trim());
            return tags;
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c.ToString(), "");
            return name;
        }
    }
}
