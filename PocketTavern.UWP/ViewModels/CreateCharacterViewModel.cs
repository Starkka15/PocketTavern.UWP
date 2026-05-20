using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using PocketTavern.UWP.Models;
using PocketTavern.UWP.Services;

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
        private string _avatarBase64 = null;
        private string _avatarPrompt = "";
        private bool _isGeneratingAvatar = false;
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
        public string AvatarBase64            { get => _avatarBase64;   set => Set(ref _avatarBase64, value); }
        public string AvatarPrompt            { get => _avatarPrompt;   set => Set(ref _avatarPrompt, value); }
        public bool   IsGeneratingAvatar      { get => _isGeneratingAvatar; set => Set(ref _isGeneratingAvatar, value); }
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
            else if (!string.IsNullOrEmpty(AvatarBase64))
            {
                var avatarName = SanitizeFileName(Name.Trim()) + ".png";
                await App.Characters.SaveAvatarFromBytesAsync(Convert.FromBase64String(AvatarBase64), avatarName);
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
            else if (!string.IsNullOrEmpty(AvatarBase64))
            {
                avatarPath = fileName + ".png";
                await App.Characters.SaveAvatarFromBytesAsync(Convert.FromBase64String(AvatarBase64), avatarPath);
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

        public async Task GenerateAvatarAsync()
        {
            if (IsGeneratingAvatar) return;
            IsGeneratingAvatar = true;
            AvatarBase64 = null;
            try
            {
                var prompt = !string.IsNullOrWhiteSpace(AvatarPrompt)
                    ? AvatarPrompt
                    : BuildDefaultAvatarPrompt();
                var imgSvc = new ImageGenService(App.Settings);
                var genParams = imgSvc.BuildParams(prompt);
                string resultBase64 = null;
                var progress = new Progress<GenerationState>(s =>
                {
                    if (s is GenerationState.Complete c) resultBase64 = c.ImageBase64;
                });
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                await imgSvc.GenerateAsync(genParams, progress, cts.Token);
                AvatarBase64 = resultBase64;
            }
            catch { }
            finally { IsGeneratingAvatar = false; }
        }

        private string BuildDefaultAvatarPrompt()
        {
            var name = Name?.Trim() ?? "";
            var desc = Description?.Trim() ?? "";
            var snippet = desc.Length > 100 ? desc.Substring(0, 100) : desc;
            return string.IsNullOrEmpty(snippet)
                ? $"portrait of {name}, high quality, detailed, fantasy character art"
                : $"portrait of {name}, {snippet}, high quality, detailed, fantasy character art";
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
