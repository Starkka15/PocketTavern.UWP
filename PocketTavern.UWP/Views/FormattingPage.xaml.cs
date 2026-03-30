using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class FormattingPage : Page
    {
        private readonly FormattingViewModel _vm = new FormattingViewModel();
        public FormattingPage() { this.InitializeComponent(); }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await _vm.LoadAsync();
            InstructCombo.ItemsSource   = _vm.InstructPresets;
            ContextCombo.ItemsSource    = _vm.ContextPresets;
            SyspromptCombo.ItemsSource  = _vm.SyspromptPresets;
            if (!string.IsNullOrEmpty(_vm.SelectedInstruct))  InstructCombo.SelectedItem  = _vm.SelectedInstruct;
            if (!string.IsNullOrEmpty(_vm.SelectedContext))   ContextCombo.SelectedItem   = _vm.SelectedContext;
            if (!string.IsNullOrEmpty(_vm.SelectedSysprompt)) SyspromptCombo.SelectedItem = _vm.SelectedSysprompt;
            CustomSysPromptBox.Text     = _vm.CustomSystemPrompt;
        }

        private void OnBackClick(object sender, RoutedEventArgs e) => App.Navigation.GoBack();
        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            _vm.SelectedInstruct   = InstructCombo.SelectedItem as string ?? "";
            _vm.SelectedContext    = ContextCombo.SelectedItem as string ?? "";
            _vm.SelectedSysprompt  = SyspromptCombo.SelectedItem as string ?? "";
            _vm.CustomSystemPrompt = CustomSysPromptBox.Text;
            _vm.Save();
        }
    }
}
