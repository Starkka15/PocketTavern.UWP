using Windows.UI.Xaml.Controls;

namespace PocketTavern.UWP.Views
{
    public sealed partial class SetupGuidePage : Page
    {
        public SetupGuidePage()
        {
            InitializeComponent();
        }

        private void BackButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }
}
