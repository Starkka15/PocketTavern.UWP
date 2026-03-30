using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class CharacterSettingsPage : Page
    {
        private readonly CharacterSettingsViewModel _vm = new CharacterSettingsViewModel();

        public CharacterSettingsPage() { this.InitializeComponent(); }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await _vm.InitializeAsync(e.Parameter as string ?? "");
            NameBox.Text           = _vm.Name;
            DescBox.Text           = _vm.Description;
            PersonalityBox.Text    = _vm.Personality;
            ScenarioBox.Text       = _vm.Scenario;
            FirstMsgBox.Text       = _vm.FirstMessage;
            MsgExampleBox.Text     = _vm.MessageExample;
            SysPromptBox.Text      = _vm.SystemPrompt;
            PostHistoryBox.Text    = _vm.PostHistoryInstructions;
            TagsBox.Text           = _vm.TagsText;
            FavoriteCheck.IsChecked = _vm.IsFavorite;

            DepthPromptBox.Text    = _vm.DepthPrompt;
            DepthBox.Text          = _vm.DepthPromptDepth.ToString();
            DepthRoleCombo.SelectedIndex = _vm.DepthPromptRole == "user" ? 1
                                         : _vm.DepthPromptRole == "assistant" ? 2 : 0;

            TalkSlider.Value       = _vm.Talkativeness;
            TalkLabel.Text         = _vm.Talkativeness.ToString("0.00");
        }

        private void OnTalkSliderChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
            => TalkLabel.Text = e.NewValue.ToString("0.00");

        private void OnBackClick(object sender, RoutedEventArgs e) => App.Navigation.GoBack();

        private async void OnSaveClick(object sender, RoutedEventArgs e)
        {
            _vm.Name                    = NameBox.Text;
            _vm.Description             = DescBox.Text;
            _vm.Personality             = PersonalityBox.Text;
            _vm.Scenario                = ScenarioBox.Text;
            _vm.FirstMessage            = FirstMsgBox.Text;
            _vm.MessageExample          = MsgExampleBox.Text;
            _vm.SystemPrompt            = SysPromptBox.Text;
            _vm.PostHistoryInstructions = PostHistoryBox.Text;
            _vm.TagsText                = TagsBox.Text;
            _vm.IsFavorite              = FavoriteCheck.IsChecked ?? false;

            _vm.DepthPrompt             = DepthPromptBox.Text;
            if (int.TryParse(DepthBox.Text, out int depth)) _vm.DepthPromptDepth = depth;
            _vm.DepthPromptRole = DepthRoleCombo.SelectedIndex == 1 ? "user"
                                : DepthRoleCombo.SelectedIndex == 2 ? "assistant"
                                : "system";

            _vm.Talkativeness           = (float)TalkSlider.Value;

            await _vm.SaveAsync();
        }

        private async void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            var dialog = new Windows.UI.Popups.MessageDialog(
                $"Delete {_vm.Name}? This cannot be undone.", "Delete Character");
            dialog.Commands.Add(new Windows.UI.Popups.UICommand("Delete", async _ => await _vm.DeleteAsync()));
            dialog.Commands.Add(new Windows.UI.Popups.UICommand("Cancel"));
            dialog.DefaultCommandIndex = 1;
            await dialog.ShowAsync();
        }
    }
}
