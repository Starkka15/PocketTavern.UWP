using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.ViewModels
{
    public class OaiPresetViewModel : ViewModelBase
    {
        private ObservableCollection<string> _presetNames = new ObservableCollection<string>();
        private string _selectedPreset = "";
        private OaiPreset _current = new OaiPreset { Name = "Default" };
        private ObservableCollection<OaiPromptOrderItemViewModel> _promptOrderItems = new ObservableCollection<OaiPromptOrderItemViewModel>();

        public ObservableCollection<string> PresetNames => _presetNames;
        public string SelectedPreset => _selectedPreset;
        public OaiPreset Current { get => _current; set => Set(ref _current, value); }
        public ObservableCollection<OaiPromptOrderItemViewModel> PromptOrderItems => _promptOrderItems;

        // ── Sampling ──────────────────────────────────────────────────────────────
        public float Temperature          { get => Current.Temperature;          set { Current.Temperature          = value; OnPropertyChanged(); } }
        public bool  TemperatureEnabled   { get => Current.TemperatureEnabled;   set { Current.TemperatureEnabled   = value; OnPropertyChanged(); } }
        public float TopP                 { get => Current.TopP;                 set { Current.TopP                 = value; OnPropertyChanged(); } }
        public bool  TopPEnabled          { get => Current.TopPEnabled;          set { Current.TopPEnabled          = value; OnPropertyChanged(); } }
        public int   TopK                 { get => Current.TopK;                 set { Current.TopK                 = System.Math.Max(0, System.Math.Min(200, value)); OnPropertyChanged(); } }
        public bool  TopKEnabled          { get => Current.TopKEnabled;          set { Current.TopKEnabled          = value; OnPropertyChanged(); } }
        public float MinP                 { get => Current.MinP;                 set { Current.MinP                 = value; OnPropertyChanged(); } }
        public bool  MinPEnabled          { get => Current.MinPEnabled;          set { Current.MinPEnabled          = value; OnPropertyChanged(); } }
        public float TopA                 { get => Current.TopA;                 set { Current.TopA                 = value; OnPropertyChanged(); } }
        public bool  TopAEnabled          { get => Current.TopAEnabled;          set { Current.TopAEnabled          = value; OnPropertyChanged(); } }
        public int   MaxTokens            { get => Current.MaxTokens;            set { Current.MaxTokens            = System.Math.Max(1, System.Math.Min(32768, value)); OnPropertyChanged(); } }
        public bool  MaxTokensEnabled     { get => Current.MaxTokensEnabled;     set { Current.MaxTokensEnabled     = value; OnPropertyChanged(); } }

        // ── Penalties ─────────────────────────────────────────────────────────────
        public float FrequencyPenalty         { get => Current.FrequencyPenalty;         set { Current.FrequencyPenalty         = value; OnPropertyChanged(); } }
        public bool  FrequencyPenaltyEnabled  { get => Current.FrequencyPenaltyEnabled;  set { Current.FrequencyPenaltyEnabled  = value; OnPropertyChanged(); } }
        public float PresencePenalty          { get => Current.PresencePenalty;          set { Current.PresencePenalty          = value; OnPropertyChanged(); } }
        public bool  PresencePenaltyEnabled   { get => Current.PresencePenaltyEnabled;   set { Current.PresencePenaltyEnabled   = value; OnPropertyChanged(); } }
        public float RepetitionPenalty        { get => Current.RepetitionPenalty;        set { Current.RepetitionPenalty        = value; OnPropertyChanged(); } }
        public bool  RepetitionPenaltyEnabled { get => Current.RepetitionPenaltyEnabled; set { Current.RepetitionPenaltyEnabled = value; OnPropertyChanged(); } }

        // ── Context & Misc ────────────────────────────────────────────────────────
        public int  ContextSize        { get => Current.ContextSize;        set { Current.ContextSize        = System.Math.Max(512, System.Math.Min(131072, value)); OnPropertyChanged(); } }
        public bool ContextSizeEnabled { get => Current.ContextSizeEnabled; set { Current.ContextSizeEnabled = value; OnPropertyChanged(); } }
        public int  Seed               { get => Current.Seed;               set { Current.Seed               = value; OnPropertyChanged(); } }
        public bool SeedEnabled        { get => Current.SeedEnabled;        set { Current.SeedEnabled        = value; OnPropertyChanged(); } }

        public async Task LoadAsync()
        {
            var presets = await App.Presets.GetAllOaiPresetsAsync();
            _presetNames.Clear();
            foreach (var p in presets) _presetNames.Add(p.Name);

            var sel = App.Settings.GetSelectedOaiPreset();
            var name = (!string.IsNullOrEmpty(sel) && _presetNames.Contains(sel))
                ? sel
                : (_presetNames.Count > 0 ? _presetNames[0] : null);

            if (name != null)
            {
                _selectedPreset = name;
                await LoadPresetAsync(name);
            }
        }

        public async Task SelectPresetAsync(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            _selectedPreset = name;
            App.Settings.SetSelectedOaiPreset(name);
            await LoadPresetAsync(name);
        }

        private async Task LoadPresetAsync(string name)
        {
            var preset = await App.Presets.GetOaiPresetAsync(name);
            if (preset == null) return;
            Current = preset;
            RefreshAllProperties();
            RebuildPromptOrderItems();
        }

        private void RefreshAllProperties()
        {
            foreach (var p in new[]
            {
                nameof(Temperature),       nameof(TemperatureEnabled),
                nameof(TopP),              nameof(TopPEnabled),
                nameof(TopK),              nameof(TopKEnabled),
                nameof(MinP),              nameof(MinPEnabled),
                nameof(TopA),              nameof(TopAEnabled),
                nameof(MaxTokens),         nameof(MaxTokensEnabled),
                nameof(FrequencyPenalty),  nameof(FrequencyPenaltyEnabled),
                nameof(PresencePenalty),   nameof(PresencePenaltyEnabled),
                nameof(RepetitionPenalty), nameof(RepetitionPenaltyEnabled),
                nameof(ContextSize),       nameof(ContextSizeEnabled),
                nameof(Seed),              nameof(SeedEnabled),
            }) OnPropertyChanged(p);
        }

        private void RebuildPromptOrderItems()
        {
            _promptOrderItems.Clear();
            var order = Current.PromptOrder ?? new List<OaiPromptOrderItem>();
            var existing = new HashSet<string>(order.Select(i => i.Id));
            foreach (var def in OaiPromptOrderItem.DefaultOrder())
                if (!existing.Contains(def.Id))
                    order.Add(def);
            foreach (var item in order)
                _promptOrderItems.Add(new OaiPromptOrderItemViewModel(item));
        }

        public void MoveUp(OaiPromptOrderItemViewModel item)
        {
            var idx = _promptOrderItems.IndexOf(item);
            if (idx <= 0) return;
            _promptOrderItems.RemoveAt(idx);
            _promptOrderItems.Insert(idx - 1, item);
        }

        public void MoveDown(OaiPromptOrderItemViewModel item)
        {
            var idx = _promptOrderItems.IndexOf(item);
            if (idx < 0 || idx >= _promptOrderItems.Count - 1) return;
            _promptOrderItems.RemoveAt(idx);
            _promptOrderItems.Insert(idx + 1, item);
        }

        public void DeleteCustomItem(OaiPromptOrderItemViewModel item)
        {
            if (item.IsCustom) _promptOrderItems.Remove(item);
        }

        public void AddCustomItem(string label, string content)
        {
            var newItem = OaiPromptOrderItem.Custom(label, content);
            int insertIdx = _promptOrderItems.Count;
            for (int i = 0; i < _promptOrderItems.Count; i++)
            {
                if (_promptOrderItems[i].Item.Id == "chat_history") { insertIdx = i; break; }
            }
            _promptOrderItems.Insert(insertIdx, new OaiPromptOrderItemViewModel(newItem));
        }

        public async Task SaveAsync(string name)
        {
            Current.Name = name;
            Current.PromptOrder = _promptOrderItems.Select(i => i.Item).ToList();
            await App.Presets.SaveOaiPresetAsync(Current);
            App.Settings.SetSelectedOaiPreset(name);
            _selectedPreset = name;

            var presets = await App.Presets.GetAllOaiPresetsAsync();
            _presetNames.Clear();
            foreach (var p in presets) _presetNames.Add(p.Name);
        }

        public async Task DeleteAsync()
        {
            await App.Presets.DeleteOaiPresetAsync(Current.Name);
            var presets = await App.Presets.GetAllOaiPresetsAsync();
            _presetNames.Clear();
            foreach (var p in presets) _presetNames.Add(p.Name);
            if (_presetNames.Count > 0)
            {
                _selectedPreset = _presetNames[0];
                await LoadPresetAsync(_selectedPreset);
            }
        }
    }
}
