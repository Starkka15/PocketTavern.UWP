using System.Collections.Generic;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.Data;
using PocketTavern.UWP.Services;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class CharacterSettingsPage : Page
    {
        private readonly CharacterSettingsViewModel _vm = new CharacterSettingsViewModel();
        private bool _isFavorite = false;
        private string _avatarFileName;
        private OpenAiTtsProvider _testProvider;
        private List<TtsVoice> _ttsVoices;

        public CharacterSettingsPage() { this.InitializeComponent(); }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _avatarFileName = e.Parameter as string ?? "";
            await _vm.InitializeAsync(_avatarFileName);

            CharNameLabel.Text = _vm.CharacterName;
            SysPromptBox.Text  = _vm.SystemPrompt;

            _isFavorite = _vm.IsFavorite;
            UpdateFavoriteIcon();

            DepthPromptBox.Text = _vm.DepthPrompt;
            DepthBox.Text       = _vm.DepthPromptDepth.ToString();
            DepthRoleCombo.SelectedIndex = _vm.DepthPromptRole == "user" ? 1
                                         : _vm.DepthPromptRole == "assistant" ? 2 : 0;
            PostHistoryBox.Text = _vm.PostHistoryInstructions;

            TalkSlider.Value  = _vm.Talkativeness;
            TalkLabel.Text    = ((int)(_vm.Talkativeness * 100)).ToString() + "%";

            // Load lorebooks
            var lorebooks = await _vm.GetLorebooksAsync();
            LorebookCombo.Items.Clear();
            LorebookCombo.Items.Add(new ComboBoxItem { Content = "(None)", Tag = "" });
            foreach (var lb in lorebooks)
                LorebookCombo.Items.Add(new ComboBoxItem { Content = lb, Tag = lb });
            SelectLorebookCombo(_vm.AttachedWorldInfo);

            // Load TTS voices
            await LoadTtsVoicesAsync();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _testProvider?.Stop();
            _testProvider = null;
        }

        private async System.Threading.Tasks.Task LoadTtsVoicesAsync()
        {
            var ttsManager = new TtsManager();
            _ttsVoices = await ttsManager.GetVoicesAsync();

            TtsVoiceCombo.Items.Clear();
            TtsVoiceCombo.Items.Add(new ComboBoxItem { Content = "Default (global setting)", Tag = "" });
            foreach (var v in _ttsVoices)
                TtsVoiceCombo.Items.Add(new ComboBoxItem { Content = v.Name, Tag = v.Id });

            var savedVoiceId = TtsVoiceStorage.GetVoiceId(_avatarFileName);
            SelectTtsVoiceCombo(savedVoiceId);
        }

        private void SelectLorebookCombo(string name)
        {
            foreach (var item in LorebookCombo.Items)
            {
                if (item is ComboBoxItem cbi && (string)cbi.Tag == (name ?? ""))
                {
                    LorebookCombo.SelectedItem = cbi;
                    return;
                }
            }
            if (LorebookCombo.Items.Count > 0)
                LorebookCombo.SelectedIndex = 0;
        }

        private void SelectTtsVoiceCombo(string voiceId)
        {
            foreach (var item in TtsVoiceCombo.Items)
            {
                if (item is ComboBoxItem cbi && (string)cbi.Tag == (voiceId ?? ""))
                {
                    TtsVoiceCombo.SelectedItem = cbi;
                    return;
                }
            }
            TtsVoiceCombo.SelectedIndex = 0;
        }

        private void UpdateFavoriteIcon()
        {
            if (_isFavorite)
            {
                FavoriteIcon.Text = "\uE735";
                FavoriteIcon.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 200, 0));
            }
            else
            {
                FavoriteIcon.Text = "\uE734";
                FavoriteIcon.Foreground = (Brush)App.Current.Resources["TextSecondaryBrush"];
            }
        }

        private void OnFavoriteClick(object sender, RoutedEventArgs e)
        {
            _isFavorite = !_isFavorite;
            UpdateFavoriteIcon();
        }

        private void OnTalkSliderChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
            => TalkLabel.Text = ((int)(e.NewValue * 100)).ToString() + "%";

        private void OnEditCharacterClick(object sender, RoutedEventArgs e)
            => App.Navigation.NavigateToEditCharacter(_avatarFileName);

        private void OnBackClick(object sender, RoutedEventArgs e) => App.Navigation.GoBack();

        private async void OnSaveClick(object sender, RoutedEventArgs e)
        {
            _vm.SystemPrompt            = SysPromptBox.Text;
            _vm.PostHistoryInstructions = PostHistoryBox.Text;
            _vm.IsFavorite              = _isFavorite;

            _vm.DepthPrompt = DepthPromptBox.Text;
            if (int.TryParse(DepthBox.Text, out int depth)) _vm.DepthPromptDepth = depth;
            _vm.DepthPromptRole = DepthRoleCombo.SelectedIndex == 1 ? "user"
                                : DepthRoleCombo.SelectedIndex == 2 ? "assistant"
                                : "system";

            _vm.Talkativeness = (float)TalkSlider.Value;

            // Save lorebook selection
            _vm.AttachedWorldInfo = (LorebookCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "";

            // Save TTS voice selection
            var selectedVoiceId = (TtsVoiceCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            if (string.IsNullOrEmpty(selectedVoiceId))
                TtsVoiceStorage.ClearVoice(_avatarFileName);
            else
                TtsVoiceStorage.SetVoiceId(_avatarFileName, selectedVoiceId);

            await _vm.SaveAsync();
        }

        private void OnTestVoiceClick(object sender, RoutedEventArgs e)
        {
            _testProvider?.Stop();
            var voiceId = (TtsVoiceCombo.SelectedItem as ComboBoxItem)?.Tag as string;
            if (string.IsNullOrEmpty(voiceId))
                voiceId = null;

            var config = App.Settings.GetTtsConfig();
            _testProvider = new OpenAiTtsProvider
            {
                ApiUrl = config.OpenAiUrl ?? "",
                ApiKey = config.OpenAiKey ?? "",
                Model  = !string.IsNullOrWhiteSpace(config.OpenAiModel) ? config.OpenAiModel : "tts-1"
            };
            var _ = _testProvider.SpeakAsync("This is a test of the text to speech system.", voiceId, (float)config.Speed);
        }

        private void OnStopVoiceClick(object sender, RoutedEventArgs e)
        {
            _testProvider?.Stop();
            _testProvider = null;
        }

        private async void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            var dialog = new Windows.UI.Popups.MessageDialog(
                $"Delete {_vm.CharacterName}? This cannot be undone.", "Delete Character");
            dialog.Commands.Add(new Windows.UI.Popups.UICommand("Delete", async _ => await _vm.DeleteAsync()));
            dialog.Commands.Add(new Windows.UI.Popups.UICommand("Cancel"));
            dialog.DefaultCommandIndex = 1;
            await dialog.ShowAsync();
        }
    }
}
