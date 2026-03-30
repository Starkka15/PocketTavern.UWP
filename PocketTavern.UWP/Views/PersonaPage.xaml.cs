using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class PersonaPage : Page
    {
        private readonly PersonaViewModel _vm = new PersonaViewModel();
        public PersonaPage() { this.InitializeComponent(); }

        protected override void OnNavigatedTo(NavigationEventArgs e)
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
        }

        private void OnNameChanged(object sender, TextChangedEventArgs e)
            => UpdateAvatarInitial(NameBox.Text);

        private void UpdateAvatarInitial(string name)
            => AvatarInitial.Text = name?.Length > 0 ? name[0].ToString().ToUpper() : "U";

        private void OnPositionChanged(object sender, SelectionChangedEventArgs e)
            => UpdateDepthRowVisibility(PositionCombo.SelectedIndex);

        private void UpdateDepthRowVisibility(int position)
            => DepthRow.Visibility = position == 1 ? Visibility.Visible : Visibility.Collapsed;

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
