using System;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class CharaVaultPage : Page
    {
        private readonly CharaVaultViewModel _vm = new CharaVaultViewModel();

        public CharaVaultPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _vm.Load();
            _vm.PropertyChanged += OnVmPropertyChanged;
            StatusLabel.Text  = _vm.StatusText;
            FooterStatus.Text = _vm.StatusText;
            UpdateLoginIcon();
        }

        private void UpdateLoginIcon()
        {
            var isLoggedIn = !string.IsNullOrEmpty(App.Settings.GetCharaVaultToken());
            LoginIcon.Text = isLoggedIn ? "\xE8F8" : "\xE77B";
            LoginButton.Foreground = isLoggedIn
                ? (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["AccentPrimaryBrush"]
                : (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["TextSecondaryBrush"];
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }

        private void OnVmPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CharaVaultViewModel.IsLoading))
            {
                LoadingRing.IsActive = _vm.IsLoading;
                if (_vm.IsLoading)
                {
                    EmptyState.Visibility = Visibility.Collapsed;
                }
            }
            if (e.PropertyName == nameof(CharaVaultViewModel.StatusText))
            {
                StatusLabel.Text = _vm.StatusText;
                FooterStatus.Text = _vm.StatusText;
            }
            if (e.PropertyName == nameof(CharaVaultViewModel.Results))
            {
                UpdateResultsView();
            }
            if (e.PropertyName == nameof(CharaVaultViewModel.HasMore))
            {
                LoadMoreButton.Visibility = _vm.HasMore ? Visibility.Visible : Visibility.Collapsed;
                FooterStatus.Visibility = _vm.HasMore ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void UpdateResultsView()
        {
            bool hasItems = _vm.Results.Count > 0;
            ResultsList.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
            EmptyState.Visibility = (hasItems || _vm.IsLoading) ? Visibility.Collapsed : Visibility.Visible;
            if (hasItems && ResultsList.ItemsSource == null)
                ResultsList.ItemsSource = _vm.Results;
        }

        private async void OnSearchClick(object sender, RoutedEventArgs e)
        {
            _vm.SearchQuery = SearchBox.Text;
            ResultsList.ItemsSource = null;
            await _vm.SearchAsync();
            ResultsList.ItemsSource = _vm.Results;
            UpdateResultsView();
        }

        private async void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                _vm.SearchQuery = SearchBox.Text;
                ResultsList.ItemsSource = null;
                await _vm.SearchAsync();
                ResultsList.ItemsSource = _vm.Results;
                UpdateResultsView();
            }
        }

        private void OnNsfwToggleChanged(object sender, RoutedEventArgs e)
            => _vm.ShowNsfw = NsfwToggle.IsChecked == true;

        private async void OnCardClicked(object sender, ItemClickEventArgs e)
        {
            if (!(e.ClickedItem is CharaVaultCardItem item)) return;

            var panel = new StackPanel { MinWidth = 260 };

            // Avatar image
            if (!string.IsNullOrEmpty(item.AvatarUrl))
            {
                try
                {
                    var img = new Windows.UI.Xaml.Controls.Image
                    {
                        Source = new Windows.UI.Xaml.Media.Imaging.BitmapImage(new Uri(item.AvatarUrl)),
                        Width = 80, Height = 80,
                        Stretch = Windows.UI.Xaml.Media.Stretch.UniformToFill,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 8)
                    };
                    panel.Children.Add(img);
                }
                catch { }
            }

            panel.Children.Add(new TextBlock
            {
                Text = item.Author,
                Style = (Style)Application.Current.Resources["SubtitleTextStyle"],
                FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            });

            if (!string.IsNullOrEmpty(item.Tagline))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = item.Tagline,
                    Foreground = (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["TextPrimaryBrush"],
                    FontSize = 13, TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 8)
                });
            }

            if (item.Stars > 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = new string('★', item.Stars) + new string('☆', System.Math.Max(0, 5 - item.Stars)),
                    FontSize = 16,
                    Foreground = (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["AccentPrimaryBrush"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8)
                });
            }

            var dialog = new ContentDialog
            {
                Title = item.Name,
                Content = new ScrollViewer { Content = panel, MaxHeight = 420 },
                PrimaryButtonText = "Download",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Primary,
                RequestedTheme = ElementTheme.Dark
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var error = await _vm.ImportCharacterAsync(item);
                if (error != null)
                {
                    var errDialog = new ContentDialog
                    {
                        Title = "Import Failed", Content = error,
                        CloseButtonText = "OK",
                        RequestedTheme = ElementTheme.Dark
                    };
                    await errDialog.ShowAsync();
                }
                else
                {
                    StatusLabel.Text = $"Imported {item.Name}";
                }
            }
        }

        private async void OnImportClick(object sender, RoutedEventArgs e)
        {
            if (!((sender as Button)?.Tag is CharaVaultCardItem item)) return;
            var error = await _vm.ImportCharacterAsync(item);
            if (error != null)
            {
                var dialog = new ContentDialog
                {
                    Title = "Import Failed",
                    Content = error,
                    CloseButtonText = "OK",
                    RequestedTheme = Windows.UI.Xaml.ElementTheme.Dark
                };
                await dialog.ShowAsync();
            }
            else
            {
                StatusLabel.Text = $"Imported {item.Name}";
            }
        }

        private async void OnLoadMoreClick(object sender, RoutedEventArgs e)
            => await _vm.LoadMoreAsync();

        private void OnBackClick(object sender, RoutedEventArgs e)
            => App.Navigation.GoBack();

        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        private async void OnLoginClick(object sender, RoutedEventArgs e)
        {
            var isLoggedIn = !string.IsNullOrEmpty(App.Settings.GetCharaVaultToken());

            if (isLoggedIn)
            {
                var confirm = new ContentDialog
                {
                    Title = "Log Out",
                    Content = $"Logged in as {App.Settings.GetCharaVaultEmail() ?? "unknown"}. Log out?",
                    PrimaryButtonText = "Log Out",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    RequestedTheme = ElementTheme.Dark
                };
                if (await confirm.ShowAsync() == ContentDialogResult.Primary)
                {
                    App.Settings.ClearCharaVaultSession();
                    UpdateLoginIcon();
                    StatusLabel.Text = "Logged out";
                }
                return;
            }

            var emailBox = new TextBox
            {
                PlaceholderText = "Email",
                Style = (Style)Application.Current.Resources["DarkTextBoxStyle"],
                Margin = new Thickness(0, 0, 0, 8)
            };
            var passwordBox = new PasswordBox
            {
                PlaceholderText = "Password or App Password (cv_...)",
                Background = (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["BackgroundSurfaceBrush"],
                Foreground = (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["TextPrimaryBrush"],
                BorderThickness = new Thickness(0)
            };
            var errorText = new TextBlock
            {
                Foreground = (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["AccentPrimaryBrush"],
                FontSize = 12, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 0)
            };
            var panel = new StackPanel();
            panel.Children.Add(emailBox);
            panel.Children.Add(passwordBox);
            panel.Children.Add(errorText);

            var dialog = new ContentDialog
            {
                Title = "Log In to CharaVault",
                Content = panel,
                PrimaryButtonText = "Log In",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                RequestedTheme = ElementTheme.Dark
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            var email = emailBox.Text.Trim();
            var password = passwordBox.Password;
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password)) return;

            var baseUrl = _vm.GetBaseUrl();
            try
            {
                var body = Newtonsoft.Json.JsonConvert.SerializeObject(new { email, password });
                var resp = await _http.PostAsync(
                    $"{baseUrl}/api/auth/login",
                    new StringContent(body, Encoding.UTF8, "application/json"));

                var json = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    var msg = JObject.Parse(json)?["detail"]?.ToString() ?? resp.ReasonPhrase;
                    var errDialog = new ContentDialog
                    {
                        Title = "Login Failed", Content = msg,
                        CloseButtonText = "OK", RequestedTheme = ElementTheme.Dark
                    };
                    await errDialog.ShowAsync();
                    return;
                }

                var obj = JObject.Parse(json);
                var token = obj["token"]?.ToString();
                var displayEmail = obj["user"]?["email"]?.ToString() ?? email;
                App.Settings.SaveCharaVaultSession(token, displayEmail);
                UpdateLoginIcon();
                StatusLabel.Text = $"Logged in as {displayEmail}";
            }
            catch (Exception ex)
            {
                var errDialog = new ContentDialog
                {
                    Title = "Login Error", Content = ex.Message,
                    CloseButtonText = "OK", RequestedTheme = ElementTheme.Dark
                };
                await errDialog.ShowAsync();
            }
        }

        private async void OnConfigureClick(object sender, RoutedEventArgs e)
        {
            var currentUrl = App.Settings.GetCharaVaultUrl();
            var box = new TextBox
            {
                Text = currentUrl,
                PlaceholderText = "https://charavault.net  (leave blank for default)",
                Style = (Windows.UI.Xaml.Style)Application.Current.Resources["DarkTextBoxStyle"]
            };
            var dialog = new ContentDialog
            {
                Title = "Card Search Server",
                Content = box,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                RequestedTheme = ElementTheme.Dark
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                App.Settings.SaveCharaVaultUrl(box.Text.Trim());
                _vm.Load();
                StatusLabel.Text = _vm.StatusText;
            }
        }
    }
}
