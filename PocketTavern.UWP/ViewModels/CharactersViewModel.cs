using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PocketTavern.UWP.Models;
using PocketTavern.UWP.Services;
using Windows.Storage;

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
        private bool _showTranslateDialog = false;
        private bool _isTranslating = false;
        private string _translateError = "";
        private Character _pendingTranslateCharacter;
        private string _pendingTranslateFileName;

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

        public bool ShowTranslateDialog
        {
            get => _showTranslateDialog;
            set => Set(ref _showTranslateDialog, value);
        }

        public bool IsTranslating
        {
            get => _isTranslating;
            set => Set(ref _isTranslating, value);
        }

        public string TranslateError
        {
            get => _translateError;
            set => Set(ref _translateError, value);
        }

        private List<Character> _allCharacters = new List<Character>();

        public async Task LoadAsync()
        {
            IsLoading = true;
            _allCharacters = await App.Characters.GetAllCharactersAsync();
            FilterCharacters();
            IsLoading = false;
        }

        /// <summary>
        /// Import a character card (PNG or .charx). If non-ASCII ratio > 0.15, offer translation dialog. (V8)
        /// </summary>
        public async Task ImportLocalCharacterAsync(StorageFile file)
        {
            IsLoading = true;
            TranslateError = "";
            try
            {
                var importedFileName = await App.Characters.ImportCharacterCardAsync(file);

                _allCharacters = await App.Characters.GetAllCharactersAsync();
                FilterCharacters();
                IsLoading = false;

                // Lang detect — load directly by fileName to avoid list-search mismatches
                var imported = await App.Characters.GetCharacterAsync(importedFileName);
                if (imported != null && ShouldOfferTranslation(imported))
                {
                    _pendingTranslateCharacter = imported;
                    _pendingTranslateFileName = importedFileName;
                    ShowTranslateDialog = true;
                }
            }
            catch (Exception ex)
            {
                IsLoading = false;
                TranslateError = ex.Message;
            }
        }

        /// <summary>Translate selected fields then save the card in place. (V9)</summary>
        public async Task TranslateAsync(IList<string> fields)
        {
            if (_pendingTranslateCharacter == null) return;
            ShowTranslateDialog = false;
            IsTranslating = true;
            TranslateError = "";
            try
            {
                var config = App.Settings.GetLlmConfig();
                var translated = await TranslateCardService.TranslateAsync(
                    _pendingTranslateCharacter, fields, config);
                await App.Characters.SaveCharacterAsync(_pendingTranslateFileName, translated);
                _allCharacters = await App.Characters.GetAllCharactersAsync();
                FilterCharacters();
            }
            catch (Exception ex)
            {
                TranslateError = ex.Message;
            }
            finally
            {
                IsTranslating = false;
                _pendingTranslateCharacter = null;
                _pendingTranslateFileName = null;
            }
        }

        public void SkipTranslation()
        {
            ShowTranslateDialog = false;
            _pendingTranslateCharacter = null;
            _pendingTranslateFileName = null;
        }

        private static bool ShouldOfferTranslation(Character ch)
        {
            var text = (ch.Description ?? "") + (ch.FirstMessage ?? "");
            if (text.Length == 0) return false;
            int nonAscii = text.Count(c => c > 127);
            return (double)nonAscii / text.Length > 0.15;
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
