using System.Collections.ObjectModel;
using System.Threading.Tasks;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.ViewModels
{
    public class CreateCharacterViewModel : ViewModelBase
    {
        private string _name = "";
        private string _description = "";
        private string _personality = "";
        private string _scenario = "";
        private string _firstMessage = "";
        private string _messageExample = "";
        private string _creatorNotes = "";
        private string _systemPrompt = "";
        private string _tagsText = "";
        private string _avatarFilePath = "";
        private bool _isSaving = false;

        public string Name           { get => _name; set => Set(ref _name, value); }
        public string Description    { get => _description; set => Set(ref _description, value); }
        public string Personality    { get => _personality; set => Set(ref _personality, value); }
        public string Scenario       { get => _scenario; set => Set(ref _scenario, value); }
        public string FirstMessage   { get => _firstMessage; set => Set(ref _firstMessage, value); }
        public string MessageExample { get => _messageExample; set => Set(ref _messageExample, value); }
        public string CreatorNotes   { get => _creatorNotes; set => Set(ref _creatorNotes, value); }
        public string SystemPrompt   { get => _systemPrompt; set => Set(ref _systemPrompt, value); }
        public string TagsText        { get => _tagsText; set => Set(ref _tagsText, value); }
        public string AvatarFilePath  { get => _avatarFilePath; set => Set(ref _avatarFilePath, value); }
        public bool IsSaving          { get => _isSaving; set => Set(ref _isSaving, value); }

        public bool CanSave => !string.IsNullOrWhiteSpace(Name);

        public async Task SaveAsync()
        {
            if (!CanSave) return;
            IsSaving = true;

            var tags = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(TagsText))
                foreach (var t in TagsText.Split(','))
                    if (!string.IsNullOrWhiteSpace(t)) tags.Add(t.Trim());

            var fileName = SanitizeFileName(Name.Trim());

            // If an avatar image was picked, copy it to the avatars folder first
            string avatarPath = null;
            if (!string.IsNullOrEmpty(AvatarFilePath) && System.IO.File.Exists(AvatarFilePath))
            {
                var ext = System.IO.Path.GetExtension(AvatarFilePath);
                avatarPath = fileName + ext;
                await App.Characters.CopyAvatarAsync(AvatarFilePath, avatarPath);
            }

            var character = new Character
            {
                Name = Name.Trim(),
                Description = Description,
                Personality = Personality,
                Scenario = Scenario,
                FirstMessage = FirstMessage,
                MessageExample = MessageExample,
                CreatorNotes = CreatorNotes,
                SystemPrompt = SystemPrompt,
                Tags = tags,
                Avatar = avatarPath ?? fileName
            };

            await App.Characters.SaveCharacterAsync(fileName, character);

            IsSaving = false;
            App.Navigation.GoBack();
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c.ToString(), "");
            return name;
        }
    }
}
