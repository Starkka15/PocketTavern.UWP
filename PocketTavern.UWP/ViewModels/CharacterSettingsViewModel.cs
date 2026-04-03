using System.Collections.Generic;
using System.Threading.Tasks;
using PocketTavern.UWP.Data;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.ViewModels
{
    public class CharacterSettingsViewModel : ViewModelBase
    {
        private string _avatarFileName;
        private Character _character;
        private string _systemPrompt = "";
        private bool _isFavorite = false;
        private float _talkativeness = 0.5f;
        private bool _isSaving = false;

        public string CharacterName  { get; private set; } = "";
        public string SystemPrompt   { get => _systemPrompt; set => Set(ref _systemPrompt, value); }
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

        private string _attachedWorldInfo = "";
        public string AttachedWorldInfo { get => _attachedWorldInfo; set => Set(ref _attachedWorldInfo, value); }

        public Task<List<string>> GetLorebooksAsync()
            => new LoreBookStorage().ListLorebooksAsync();

        public async Task InitializeAsync(string avatarUrl)
        {
            _avatarFileName = avatarUrl;
            _character = await App.Characters.GetCharacterAsync(avatarUrl);
            if (_character == null) return;

            CharacterName           = _character.Name;
            SystemPrompt            = _character.SystemPrompt;
            PostHistoryInstructions = _character.PostHistoryInstructions;
            DepthPrompt             = _character.DepthPrompt;
            DepthPromptDepth        = _character.DepthPromptDepth;
            DepthPromptRole         = _character.DepthPromptRole;
            IsFavorite              = _character.IsFavorite;
            Talkativeness           = _character.Talkativeness;
            AttachedWorldInfo       = _character.AttachedWorldInfo ?? "";
        }

        public async Task SaveAsync()
        {
            IsSaving = true;

            _character.SystemPrompt            = SystemPrompt;
            _character.PostHistoryInstructions = PostHistoryInstructions;
            _character.DepthPrompt             = DepthPrompt;
            _character.DepthPromptDepth        = DepthPromptDepth;
            _character.DepthPromptRole         = DepthPromptRole;
            _character.IsFavorite              = IsFavorite;
            _character.Talkativeness           = Talkativeness;
            _character.AttachedWorldInfo       = string.IsNullOrEmpty(AttachedWorldInfo) ? null : AttachedWorldInfo;

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
