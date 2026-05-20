using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PocketTavern.UWP.Data;
using PocketTavern.UWP.Models;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace PocketTavern.UWP.Views
{
    public class LorebookEntryViewModel
    {
        public WorldInfoEntry Entry { get; set; }
        public string DisplayComment => string.IsNullOrWhiteSpace(Entry.Comment) ? "(no title)" : Entry.Comment;
        public string DisplayKeys => Entry.Keys.Count > 0
            ? "Keys: " + string.Join(", ", Entry.Keys)
            : "No trigger keys";
    }

    public sealed partial class LorebookEntriesPage : Page
    {
        private readonly LoreBookStorage _storage = new LoreBookStorage();
        private string _lorebookName;
        private List<WorldInfoEntry> _entries = new List<WorldInfoEntry>();
        private readonly ObservableCollection<LorebookEntryViewModel> _vms
            = new ObservableCollection<LorebookEntryViewModel>();

        public LorebookEntriesPage() { this.InitializeComponent(); }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _lorebookName = e.Parameter as string ?? "Unknown";
            TitleLabel.Text = _lorebookName;
            _entries = await _storage.LoadLorebookAsync(_lorebookName);
            Refresh();
        }

        private void Refresh()
        {
            _vms.Clear();
            foreach (var entry in _entries)
                _vms.Add(new LorebookEntryViewModel { Entry = entry });
            EntriesList.ItemsSource = _vms;
            EmptyState.Visibility = _vms.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnBackClick(object sender, RoutedEventArgs e) => App.Navigation.GoBack();

        private async void OnAddEntryClick(object sender, RoutedEventArgs e)
        {
            var entry = new WorldInfoEntry { Uid = Guid.NewGuid().ToString("N").Substring(0, 8) };
            var edited = await ShowEntryDialogAsync(entry, isNew: true);
            if (edited == null) return;
            _entries.Add(edited);
            await _storage.SaveLorebookAsync(_lorebookName, _entries);
            Refresh();
        }

        private async void OnEditEntryClick(object sender, RoutedEventArgs e)
        {
            var vm = (sender as Button)?.Tag as LorebookEntryViewModel;
            if (vm == null) return;
            var edited = await ShowEntryDialogAsync(vm.Entry, isNew: false);
            if (edited == null) return;
            var idx = _entries.IndexOf(vm.Entry);
            if (idx >= 0) _entries[idx] = edited;
            await _storage.SaveLorebookAsync(_lorebookName, _entries);
            Refresh();
        }

        private async void OnDeleteEntryClick(object sender, RoutedEventArgs e)
        {
            var vm = (sender as Button)?.Tag as LorebookEntryViewModel;
            if (vm == null) return;

            var confirm = new ContentDialog
            {
                Title = "Delete entry?",
                Content = $"Delete \"{vm.DisplayComment}\"?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel"
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

            _entries.Remove(vm.Entry);
            await _storage.SaveLorebookAsync(_lorebookName, _entries);
            Refresh();
        }

        private async System.Threading.Tasks.Task<WorldInfoEntry> ShowEntryDialogAsync(WorldInfoEntry entry, bool isNew)
        {
            var panel = new StackPanel { Spacing = 8 };

            var commentBox = new TextBox
            {
                Header = "Title / Comment",
                Text = entry.Comment ?? "",
                PlaceholderText = "Entry title"
            };

            var keysBox = new TextBox
            {
                Header = "Trigger keys (comma-separated)",
                Text = string.Join(", ", entry.Keys ?? new List<string>()),
                PlaceholderText = "keyword1, keyword2"
            };

            var contentBox = new TextBox
            {
                Header = "Content",
                Text = entry.Content ?? "",
                PlaceholderText = "World info content injected when triggered",
                AcceptsReturn = true,
                Height = 120,
                TextWrapping = TextWrapping.Wrap
            };

            var constantToggle = new ToggleSwitch
            {
                Header = "Always active (constant)",
                IsOn = entry.Constant
            };

            panel.Children.Add(commentBox);
            panel.Children.Add(keysBox);
            panel.Children.Add(contentBox);
            panel.Children.Add(constantToggle);

            var dialog = new ContentDialog
            {
                Title = isNew ? "New Entry" : "Edit Entry",
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

            return new WorldInfoEntry
            {
                Uid            = entry.Uid,
                Comment        = commentBox.Text.Trim(),
                Keys           = keys,
                SecondaryKeys  = entry.SecondaryKeys,
                Content        = contentBox.Text,
                Constant       = constantToggle.IsOn,
                Selective      = entry.Selective,
                Order          = entry.Order,
                Position       = entry.Position,
                Depth          = entry.Depth,
                Probability    = entry.Probability,
                Enabled        = entry.Enabled,
                Group          = entry.Group,
                ScanDepth      = entry.ScanDepth,
                CaseSensitive  = entry.CaseSensitive,
                MatchWholeWords = entry.MatchWholeWords
            };
        }
    }
}
