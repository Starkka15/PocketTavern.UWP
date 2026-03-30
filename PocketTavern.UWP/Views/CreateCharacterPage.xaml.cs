using System;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class CreateCharacterPage : Page
    {
        private readonly CreateCharacterViewModel _vm = new CreateCharacterViewModel();

        public CreateCharacterPage() { this.InitializeComponent(); }

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
                AvatarImage.Source  = bmp;
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

                // Navigate back after successful import
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
            _vm.Name        = NameBox.Text;
            _vm.Description = DescBox.Text;
            _vm.Personality = PersonalityBox.Text;
            _vm.Scenario    = ScenarioBox.Text;
            _vm.FirstMessage= FirstMsgBox.Text;
            _vm.SystemPrompt= SysPromptBox.Text;
            _vm.TagsText    = TagsBox.Text;
            await _vm.SaveAsync();
        }
    }
}
