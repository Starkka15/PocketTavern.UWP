using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class ContextSettingsPage : Page
    {
        private readonly ContextSettingsViewModel _vm = new ContextSettingsViewModel();

        public ContextSettingsPage() { this.InitializeComponent(); }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _vm.Load();

            AuthorsNoteBox.Text          = _vm.AuthorsNoteContent;
            NotePositionCombo.SelectedIndex = _vm.AuthorsNotePosition;
            NoteDepthBox.Text            = _vm.AuthorsNoteDepth.ToString();
            NoteIntervalBox.Text         = _vm.AuthorsNoteInterval.ToString();
            NoteRoleCombo.SelectedIndex  = _vm.AuthorsNoteRole;
            UpdateNoteDepthVisibility(_vm.AuthorsNotePosition);
            UpdateIntervalHint(_vm.AuthorsNoteInterval);

            AutoContinueToggle.IsOn = _vm.AutoContinueEnabled;
            AutoContinueMinLengthBox.Text = _vm.AutoContinueMinLength.ToString();
            UpdateAutoContinueMinLengthVisibility(_vm.AutoContinueEnabled);
        }

        private void OnNotePositionChanged(object sender, SelectionChangedEventArgs e)
            => UpdateNoteDepthVisibility(NotePositionCombo.SelectedIndex);

        private void UpdateNoteDepthVisibility(int position)
            => NoteDepthRow.Visibility = position == 1 ? Visibility.Visible : Visibility.Collapsed;

        private void OnAutoContinueToggled(object sender, RoutedEventArgs e)
            => UpdateAutoContinueMinLengthVisibility(AutoContinueToggle.IsOn);

        private void UpdateAutoContinueMinLengthVisibility(bool enabled)
            => AutoContinueMinLengthRow.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;

        private void UpdateIntervalHint(int interval)
            => NoteIntervalHint.Text = interval <= 1 ? "every message" : $"every {interval} messages";

        private void OnBackClick(object sender, RoutedEventArgs e) => App.Navigation.GoBack();

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            _vm.AuthorsNoteContent  = AuthorsNoteBox.Text;
            _vm.AuthorsNotePosition = NotePositionCombo.SelectedIndex;
            if (int.TryParse(NoteDepthBox.Text,    out int d)) _vm.AuthorsNoteDepth    = d;
            if (int.TryParse(NoteIntervalBox.Text, out int i)) _vm.AuthorsNoteInterval = System.Math.Max(0, System.Math.Min(100, i));
            _vm.AuthorsNoteRole = NoteRoleCombo.SelectedIndex;

            _vm.AutoContinueEnabled = AutoContinueToggle.IsOn;
            if (int.TryParse(AutoContinueMinLengthBox.Text, out int ml))
                _vm.AutoContinueMinLength = System.Math.Max(0, System.Math.Min(5000, ml));

            _vm.Save();
        }
    }
}
