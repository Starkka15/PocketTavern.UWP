using System.Threading.Tasks;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.ViewModels
{
    public class CharacterSettingsViewModel : ViewModelBase
    {
        private string _avatarFileName;
        private Character _character;
        private string _name = "";
        private string _description = "";
        private string _personality = "";
        private string _scenario = "";
        private string _firstMessage = "";
        private string _messageExample = "";
        private string _creatorNotes = "";
        private string _systemPrompt = "";
        private string _tagsText = "";
        private bool _isFavorite = false;
        private float _talkativeness = 0.5f;
        private bool _isSaving = false;

        public string Name           { get => _name; set => Set(ref _name, value); }
        public string Description    { get => _description; set => Set(ref _description, value); }
        public string Personality    { get => _personality; set => Set(ref _personality, value); }
        public string Scenario       { get => _scenario; set => Set(ref _scenario, value); }
        public string FirstMessage   { get => _firstMessage; set => Set(ref _firstMessage, value); }
        public string MessageExample { get => _messageExample; set => Set(ref _messageExample, value); }
        public string CreatorNotes   { get => _creatorNotes; set => Set(ref _creatorNotes, value); }
        public string SystemPrompt   { get => _systemPrompt; set => Set(ref _systemPrompt, value); }
        public string TagsText       { get => _tagsText; set => Set(ref _tagsText, value); }
        public bool IsFavorite       { get => _isFavorite; set => Set(ref _isFavorite, value); }
        public float Talkativeness   { get => _talkativeness; set => Set(ref _talkativeness, value); }
        public bool IsSaving         { get => _isSaving; set => Set(ref _isSaving, value); }

        private string _postHistoryInstructions = "";
        private string _depthPrompt = "";
        private int _depthPromptDepth = 4;
        private string _depthPromptRole = "system";

        public string PostHistoryInstructions { get => _postHistoryInstructions; set => Set(ref _postHistoryInstructions, value); }
        public string DepthPrompt             { get => _depthPrompt;             set => Set(ref _depthPrompt, value); }
        public int    DepthPromptDepth        { get => _depthPromptDepth;        set => Set(ref _depthPromptDepth, value); }
        public string DepthPromptRole         { get => _depthPromptRole;         set => Set(ref _depthPromptRole, value); }

        public async Task InitializeAsync(string avatarUrl)
        {
            _avatarFileName = avatarUrl;
            _character = await App.Characters.GetCharacterAsync(avatarUrl);
            if (_character == null) return;

            Name                    = _character.Name;
            Description             = _character.Description;
            Personality             = _character.Personality;
            Scenario                = _character.Scenario;
            FirstMessage            = _character.FirstMessage;
            MessageExample          = _character.MessageExample;
            CreatorNotes            = _character.CreatorNotes;
            SystemPrompt            = _character.SystemPrompt;
            PostHistoryInstructions = _character.PostHistoryInstructions;
            DepthPrompt             = _character.DepthPrompt;
            DepthPromptDepth        = _character.DepthPromptDepth;
            DepthPromptRole         = _character.DepthPromptRole;
            TagsText = string.Join(", ", _character.Tags ?? new System.Collections.Generic.List<string>());
            IsFavorite              = _character.IsFavorite;
            Talkativeness           = _character.Talkativeness;
        }

        public async Task SaveAsync()
        {
            IsSaving = true;
            var tags = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(TagsText))
                foreach (var t in TagsText.Split(','))
                    if (!string.IsNullOrWhiteSpace(t)) tags.Add(t.Trim());

            _character.Name                    = Name;
            _character.Description             = Description;
            _character.Personality             = Personality;
            _character.Scenario                = Scenario;
            _character.FirstMessage            = FirstMessage;
            _character.MessageExample          = MessageExample;
            _character.CreatorNotes            = CreatorNotes;
            _character.SystemPrompt            = SystemPrompt;
            _character.PostHistoryInstructions = PostHistoryInstructions;
            _character.DepthPrompt             = DepthPrompt;
            _character.DepthPromptDepth        = DepthPromptDepth;
            _character.DepthPromptRole         = DepthPromptRole;
            _character.Tags                    = tags;
            _character.IsFavorite              = IsFavorite;
            _character.Talkativeness           = Talkativeness;

            await App.Characters.SaveCharacterAsync(_avatarFileName, _character);
            IsSaving = false;
            App.Navigation.GoBack();
        }

        public async Task DeleteAsync()
        {
            if (_avatarFileName == null) return;
            await App.Characters.DeleteCharacterAsync(_avatarFileName);
            App.Navigation.GoBack();
        }
    }
}
