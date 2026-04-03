using System;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class CreateCharacterPage : Page
    {
        private readonly CreateCharacterViewModel _vm = new CreateCharacterViewModel();

        public CreateCharacterPage() { this.InitializeComponent(); }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string fileName && !string.IsNullOrEmpty(fileName))
            {
                await _vm.LoadForEditAsync(fileName);
                PageTitle.Text             = "Edit Character";
                NameBox.Text               = _vm.Name;
                DescBox.Text               = _vm.Description;
                PersonalityBox.Text        = _vm.Personality;
                ScenarioBox.Text           = _vm.Scenario;
                FirstMsgBox.Text           = _vm.FirstMessage;
                MsgExampleBox.Text         = _vm.MessageExample;
                SysPromptBox.Text          = _vm.SystemPrompt;
                PostHistoryBox.Text        = _vm.PostHistoryInstructions;
                TagsBox.Text               = _vm.TagsText;
                CreatorNotesBox.Text       = _vm.CreatorNotes;
                AvatarInitial.Text         = _vm.Name?.Length > 0 ? _vm.Name[0].ToString().ToUpper() : "?";
                AltGreetingsList.ItemsSource = _vm.AlternateGreetings;
            }
        }

        private void OnBackClick(object sender, RoutedEventArgs e) => App.Navigation.GoBack();

        private void OnNameChanged(object sender, TextChangedEventArgs e)
        {
            var name = NameBox.Text;
            AvatarInitial.Text = name?.Length > 0 ? name[0].ToString().ToUpper() : "?";
        }

        private async void OnAvatarClick(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            _vm.AvatarFilePath = file.Path;
            try
            {
                var bmp = new BitmapImage();
                using (var stream = await file.OpenReadAsync())
                    await bmp.SetSourceAsync(stream);
                AvatarImage.Source   = bmp;
                AvatarImage.Visibility   = Visibility.Visible;
                AvatarInitial.Visibility = Visibility.Collapsed;
            }
            catch { }
        }

        private async void OnImportPngClick(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add(".png");

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            try
            {
                var bytes = (await FileIO.ReadBufferAsync(file)).ToArray();
                await App.Characters.ImportCharacterFromBytesAsync(
                    System.IO.Path.GetFileNameWithoutExtension(file.Name), bytes);

                App.Navigation.GoBack();
            }
            catch (Exception ex)
            {
                var dialog = new Windows.UI.Popups.MessageDialog(
                    "Failed to import card: " + ex.Message, "Import Error");
                await dialog.ShowAsync();
            }
        }

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
            _vm.CreatorNotes            = CreatorNotesBox.Text;
            await _vm.SaveAsync();
        }

        // Alternate Greetings management
        private void OnAddAltGreetingClick(object sender, RoutedEventArgs e)
        {
            _vm.AlternateGreetings.Add("");
            AltGreetingsList.ItemsSource = null;
            AltGreetingsList.ItemsSource = _vm.AlternateGreetings;
        }

        private void OnRemoveAltGreetingClick(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is string greeting)
            {
                _vm.AlternateGreetings.Remove(greeting);
                AltGreetingsList.ItemsSource = null;
                AltGreetingsList.ItemsSource = _vm.AlternateGreetings;
            }
        }

        private void OnAltGreetingChanged(object sender, TextChangedEventArgs e)
        {
            // Update value in collection when text changes
            if (sender is TextBox tb && tb.Tag is string oldValue)
            {
                var idx = _vm.AlternateGreetings.IndexOf(oldValue);
                if (idx >= 0)
                {
                    _vm.AlternateGreetings[idx] = tb.Text;
                    tb.Tag = tb.Text;
                }
            }
        }
    }
}
