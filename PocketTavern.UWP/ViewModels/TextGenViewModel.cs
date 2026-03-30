using System.Collections.ObjectModel;
using System.Threading.Tasks;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.ViewModels
{
    public class TextGenViewModel : ViewModelBase
    {
        private ObservableCollection<string> _presetNames = new ObservableCollection<string>();
        private string _selectedPreset = "";
        private TextGenPreset _current = new TextGenPreset { Name = "Default" };
        private bool _isLoading = false;

        public ObservableCollection<string> PresetNames { get => _presetNames; set => Set(ref _presetNames, value); }
        public string SelectedPreset { get => _selectedPreset; set { if (Set(ref _selectedPreset, value)) _ = LoadPresetAsync(value); } }
        public TextGenPreset Current { get => _current; set => Set(ref _current, value); }
        public bool IsLoading { get => _isLoading; set => Set(ref _isLoading, value); }

        // Expose individual fields for two-way binding
        public float Temperature { get => Current.Temperature; set { Current.Temperature = value; OnPropertyChanged(); } }
        public float TopP        { get => Current.TopP; set { Current.TopP = value; OnPropertyChanged(); } }
        public int TopK          { get => Current.TopK; set { Current.TopK = value; OnPropertyChanged(); } }
        public float RepPen      { get => Current.RepPen; set { Current.RepPen = value; OnPropertyChanged(); } }
        public int? MaxNewTokens { get => Current.MaxNewTokens; set { Current.MaxNewTokens = value; OnPropertyChanged(); } }

        public async Task LoadAsync()
        {
            IsLoading = true;
            var presets = await App.Presets.GetAllTextGenPresetsAsync();
            PresetNames.Clear();
            foreach (var p in presets) PresetNames.Add(p.Name);

            var sel = App.Settings.GetSelectedTextGenPreset();
            if (!string.IsNullOrEmpty(sel))
            {
                _selectedPreset = sel;
                await LoadPresetAsync(sel);
            }
            IsLoading = false;
        }

        private async Task LoadPresetAsync(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            var preset = await App.Presets.GetTextGenPresetAsync(name);
            if (preset != null)
            {
                Current = preset;
                OnPropertyChanged(nameof(Temperature));
                OnPropertyChanged(nameof(TopP));
                OnPropertyChanged(nameof(TopK));
                OnPropertyChanged(nameof(RepPen));
                OnPropertyChanged(nameof(MaxNewTokens));
            }
        }

        public async Task SaveAsync()
        {
            await App.Presets.SaveTextGenPresetAsync(Current);
            App.Settings.SetSelectedTextGenPreset(Current.Name);
            App.Navigation.GoBack();
        }
    }
}
