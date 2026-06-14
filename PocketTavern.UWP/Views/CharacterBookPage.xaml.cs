using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PocketTavern.UWP.Data;
using PocketTavern.UWP.Models;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace PocketTavern.UWP.Views
{
    public class CharacterBookEntryViewModel
    {
        public CharacterBookEntry Entry { get; set; }
        public string DisplayName => string.IsNullOrWhiteSpace(Entry.Name)
            ? (string.IsNullOrWhiteSpace(Entry.Comment) ? "(unnamed)" : Entry.Comment)
            : Entry.Name;
        public string DisplayKeys => Entry.Keys.Count > 0
            ? "Keys: " + string.Join(", ", Entry.Keys)
            : "No trigger keys";
    }

    public sealed partial class CharacterBookPage : Page
    {
        private string _avatar;
        private Character _character;
        private List<CharacterBookEntry> _entries = new List<CharacterBookEntry>();
        private readonly ObservableCollection<CharacterBookEntryViewModel> _vms
            = new ObservableCollection<CharacterBookEntryViewModel>();

        public CharacterBookPage() { this.InitializeComponent(); }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _avatar = e.Parameter as string;
            if (string.IsNullOrEmpty(_avatar))
            {
                App.Navigation.GoBack();
                return;
            }

            _character = await App.Characters.GetCharacterAsync(_avatar);
            if (_character == null)
            {
                App.Navigation.GoBack();
                return;
            }

            TitleLabel.Text = _character.Name + " — Lore";

            if (!string.IsNullOrEmpty(_character.CharacterBookJson))
            {
                try
                {
                    var cb = JObject.Parse(_character.CharacterBookJson);
                    _entries = cb["entries"]?.ToObject<List<CharacterBookEntry>>() ?? new List<CharacterBookEntry>();
                }
                catch { }
            }

            Refresh();
        }

        private void Refresh()
        {
            _vms.Clear();
            foreach (var entry in _entries)
                _vms.Add(new CharacterBookEntryViewModel { Entry = entry });
            EntriesList.ItemsSource = _vms;
            EmptyState.Visibility = _vms.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnBackClick(object sender, RoutedEventArgs e) => App.Navigation.GoBack();

        private async void OnAddEntryClick(object sender, RoutedEventArgs e)
        {
            var entry = new CharacterBookEntry();
            var edited = await ShowEntryDialogAsync(entry, isNew: true);
            if (edited == null) return;
            _entries.Add(edited);
            await SaveAsync();
            Refresh();
        }

        private async void OnEditEntryClick(object sender, RoutedEventArgs e)
        {
            var vm = (sender as Button)?.Tag as CharacterBookEntryViewModel;
            if (vm == null) return;
            var edited = await ShowEntryDialogAsync(vm.Entry, isNew: false);
            if (edited == null) return;
            var idx = _entries.IndexOf(vm.Entry);
            if (idx >= 0) _entries[idx] = edited;
            await SaveAsync();
            Refresh();
        }

        private async void OnDeleteEntryClick(object sender, RoutedEventArgs e)
        {
            var vm = (sender as Button)?.Tag as CharacterBookEntryViewModel;
            if (vm == null) return;

            var confirm = new ContentDialog
            {
                Title = "Delete entry?",
                Content = $"Delete \"{vm.DisplayName}\"?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel"
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

            _entries.Remove(vm.Entry);
            await SaveAsync();
            Refresh();
        }

        private async System.Threading.Tasks.Task SaveAsync()
        {
            var cb = new JObject
            {
                ["entries"] = JArray.FromObject(_entries)
            };
            _character.CharacterBookJson = cb.ToString(Formatting.None);
            _character.HasCharacterBook = _entries.Count > 0;
            _character.CharacterBookEntryCount = _entries.Count;
            await App.Characters.SaveCharacterAsync(_avatar, _character);
        }

        private async System.Threading.Tasks.Task<CharacterBookEntry> ShowEntryDialogAsync(CharacterBookEntry entry, bool isNew)
        {
            var panel = new StackPanel { Spacing = 8 };

            var nameBox = new TextBox
            {
                Header = "Name",
                Text = entry.Name ?? "",
                PlaceholderText = "Entry name (optional)"
            };

            var commentBox = new TextBox
            {
                Header = "Comment",
                Text = entry.Comment ?? "",
                PlaceholderText = "Internal note about this entry"
            };

            var keysBox = new TextBox
            {
                Header = "Trigger keys (comma-separated)",
                Text = string.Join(", ", entry.Keys ?? new List<string>()),
                PlaceholderText = "keyword1, keyword2"
            };

            var secondaryKeysBox = new TextBox
            {
                Header = "Secondary keys (comma-separated, optional)",
                Text = string.Join(", ", entry.SecondaryKeys ?? new List<string>()),
                PlaceholderText = "secondary1, secondary2"
            };

            var contentBox = new TextBox
            {
                Header = "Content",
                Text = entry.Content ?? "",
                PlaceholderText = "Lore content injected when triggered",
                AcceptsReturn = true,
                Height = 120,
                TextWrapping = TextWrapping.Wrap
            };

            var enabledToggle = new ToggleSwitch
            {
                Header = "Enabled",
                IsOn = entry.Enabled
            };

            var constantToggle = new ToggleSwitch
            {
                Header = "Always active (constant)",
                IsOn = entry.Constant
            };

            var selectiveToggle = new ToggleSwitch
            {
                Header = "Selective (only match secondary keys)",
                IsOn = entry.Selective
            };

            var caseSensitiveToggle = new ToggleSwitch
            {
                Header = "Case sensitive",
                IsOn = entry.CaseSensitive
            };

            panel.Children.Add(nameBox);
            panel.Children.Add(commentBox);
            panel.Children.Add(keysBox);
            panel.Children.Add(secondaryKeysBox);
            panel.Children.Add(contentBox);
            panel.Children.Add(enabledToggle);
            panel.Children.Add(constantToggle);
            panel.Children.Add(selectiveToggle);
            panel.Children.Add(caseSensitiveToggle);

            var dialog = new ContentDialog
            {
                Title = isNew ? "New Lore Entry" : "Edit Lore Entry",
                Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 450 },
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel"
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;

            var keys = keysBox.Text
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .Where(k => k.Length > 0)
                .ToList();

            var secondaryKeys = secondaryKeysBox.Text
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .Where(k => k.Length > 0)
                .ToList();

            return new CharacterBookEntry
            {
                Id = entry.Id ?? (_entries.Count > 0 ? _entries.Max(e => e.Id ?? 0) + 1 : 1),
                Name = nameBox.Text.Trim(),
                Comment = commentBox.Text.Trim(),
                Keys = keys,
                SecondaryKeys = secondaryKeys,
                Content = contentBox.Text,
                Enabled = enabledToggle.IsOn,
                Constant = constantToggle.IsOn,
                Selective = selectiveToggle.IsOn,
                CaseSensitive = caseSensitiveToggle.IsOn,
                InsertionOrder = entry.InsertionOrder,
                Priority = entry.Priority,
                Position = entry.Position
            };
        }
    }
}
