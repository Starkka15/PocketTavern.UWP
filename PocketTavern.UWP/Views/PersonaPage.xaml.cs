using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class PersonaPage : Page
    {
        private readonly PersonaViewModel _vm = new PersonaViewModel();
        private string _avatarPath;

        public PersonaPage() { this.InitializeComponent(); }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _vm.Load();
            NameBox.Text = _vm.Name;
            DescBox.Text = _vm.Description;
            PositionCombo.SelectedIndex = _vm.Position;
            DepthBox.Text = _vm.Depth.ToString();
            RoleCombo.SelectedIndex = _vm.Role;
            UpdateAvatarInitial(_vm.Name);
            UpdateDepthRowVisibility(_vm.Position);
            await LoadAvatarAsync();
            await LoadStatsAsync();
        }

        private void OnNameChanged(object sender, TextChangedEventArgs e)
            => UpdateAvatarInitial(NameBox.Text);

        private void UpdateAvatarInitial(string name)
            => AvatarInitial.Text = name?.Length > 0 ? name[0].ToString().ToUpper() : "U";

        private void OnPositionChanged(object sender, SelectionChangedEventArgs e)
            => UpdateDepthRowVisibility(PositionCombo.SelectedIndex);

        private void UpdateDepthRowVisibility(int position)
            => DepthRow.Visibility = position == 1 ? Visibility.Visible : Visibility.Collapsed;

        private async void OnAvatarTapped(object sender, TappedRoutedEventArgs e)
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".webp");

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            try
            {
                var profileDir = Path.Combine(ApplicationData.Current.LocalFolder.Path, "profile");
                Directory.CreateDirectory(profileDir);
                var dest = await file.CopyAsync(
                    await StorageFolder.GetFolderFromPathAsync(profileDir),
                    "avatar.png",
                    NameCollisionOption.ReplaceExisting);

                _avatarPath = dest.Path;
                AvatarImage.Source = new BitmapImage(new Uri("file:///" + dest.Path.Replace('\\', '/')));
                AvatarImage.Visibility = Visibility.Visible;
                AvatarInitial.Visibility = Visibility.Collapsed;
            }
            catch { }
        }

        private async Task LoadAvatarAsync()
        {
            var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "profile", "avatar.png");
            if (File.Exists(path))
            {
                _avatarPath = path;
                AvatarImage.Source = new BitmapImage(new Uri("file:///" + path.Replace('\\', '/')));
                AvatarImage.Visibility = Visibility.Visible;
                AvatarInitial.Visibility = Visibility.Collapsed;
            }
        }

        private async Task LoadStatsAsync()
        {
            try
            {
                var chars = await App.Characters.GetAllCharactersAsync();
                CharCountText.Text = chars.Count.ToString();

                var chatDir = Path.Combine(ApplicationData.Current.LocalFolder.Path, "chats");
                int totalChats = 0;
                int totalMessages = 0;

                if (Directory.Exists(chatDir))
                {
                    foreach (var charDir in Directory.GetDirectories(chatDir))
                    {
                        var files = Directory.GetFiles(charDir, "*.jsonl")
                            .Concat(Directory.GetFiles(charDir, "*.json"));
                        totalChats += files.Count();
                        foreach (var f in files)
                        {
                            try
                            {
                                var lines = File.ReadLines(f);
                                totalMessages += lines.Count(l => !string.IsNullOrWhiteSpace(l)
                                    && !l.Contains("\"user_name\"") && !l.Contains("\"character_name\""));
                            }
                            catch { }
                        }
                    }
                }

                ChatCountText.Text = totalChats.ToString();
                MsgCountText.Text = totalMessages.ToString();
            }
            catch { }
        }

        private void OnBackClick(object sender, RoutedEventArgs e) => App.Navigation.GoBack();

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            _vm.Name = NameBox.Text;
            _vm.Description = DescBox.Text;
            _vm.Position = PositionCombo.SelectedIndex;
            if (int.TryParse(DepthBox.Text, out int d)) _vm.Depth = d;
            _vm.Role = RoleCombo.SelectedIndex;
            _vm.Save();
        }
    }
}
