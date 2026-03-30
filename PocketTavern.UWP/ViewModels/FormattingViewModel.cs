using System.Collections.ObjectModel;
using System.Threading.Tasks;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.ViewModels
{
    public class FormattingViewModel : ViewModelBase
    {
        private ObservableCollection<string> _instructPresets = new ObservableCollection<string>();
        private ObservableCollection<string> _contextPresets = new ObservableCollection<string>();
        private ObservableCollection<string> _syspromptPresets = new ObservableCollection<string>();
        private string _selectedInstruct = "";
        private string _selectedContext = "";
        private string _selectedSysprompt = "";
        private string _customSystemPrompt = "";

        public ObservableCollection<string> InstructPresets  { get => _instructPresets; set => Set(ref _instructPresets, value); }
        public ObservableCollection<string> ContextPresets   { get => _contextPresets; set => Set(ref _contextPresets, value); }
        public ObservableCollection<string> SyspromptPresets { get => _syspromptPresets; set => Set(ref _syspromptPresets, value); }
        public string SelectedInstruct  { get => _selectedInstruct; set => Set(ref _selectedInstruct, value); }
        public string SelectedContext   { get => _selectedContext; set => Set(ref _selectedContext, value); }
        public string SelectedSysprompt { get => _selectedSysprompt; set => Set(ref _selectedSysprompt, value); }
        public string CustomSystemPrompt { get => _customSystemPrompt; set => Set(ref _customSystemPrompt, value); }

        public async Task LoadAsync()
        {
            var instructs  = await App.Presets.GetAllInstructTemplatesAsync();
            var contexts   = await App.Presets.GetAllContextTemplatesAsync();
            var sysprompts = await App.Presets.GetAllSyspromptPresetsAsync();

            InstructPresets.Clear();
            foreach (var i in instructs) InstructPresets.Add(i.Name);

            ContextPresets.Clear();
            foreach (var c in contexts) ContextPresets.Add(c.Name);

            SyspromptPresets.Clear();
            foreach (var s in sysprompts) SyspromptPresets.Add(s.Name);

            SelectedInstruct   = App.Settings.GetSelectedInstructPreset() ?? "";
            SelectedContext    = App.Settings.GetSelectedContextPreset() ?? "";
            SelectedSysprompt  = App.Settings.GetSelectedSyspromptPreset() ?? "";
            CustomSystemPrompt = App.Settings.GetCustomSystemPrompt();
        }

        public void Save()
        {
            App.Settings.SetSelectedInstructPreset(SelectedInstruct);
            App.Settings.SetSelectedContextPreset(SelectedContext);
            App.Settings.SetSelectedSyspromptPreset(SelectedSysprompt);
            App.Settings.SaveCustomSystemPrompt(CustomSystemPrompt);
            App.Navigation.GoBack();
        }
    }
}
