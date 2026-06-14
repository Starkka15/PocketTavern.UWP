using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace PocketTavern.UWP.Views
{
    public class StorageItemViewModel
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public string Icon { get; set; }
        public string Detail { get; set; }
        public string Size { get; set; }
        public bool IsDirectory { get; set; }
    }

    public sealed partial class StorageBrowserPage : Page
    {
        private string _currentPath;
        private readonly ObservableCollection<StorageItemViewModel> _items
            = new ObservableCollection<StorageItemViewModel>();
        private string _localRoot;

        public StorageBrowserPage() { this.InitializeComponent(); }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _localRoot = ApplicationData.Current.LocalFolder.Path;
            _currentPath = _localRoot;
            PathLabel.Text = _currentPath;
            UpdateUpButton();
            LoadDirectory(_currentPath);
        }

        private void LoadDirectory(string path)
        {
            _items.Clear();

            try
            {
                foreach (var dir in Directory.GetDirectories(path))
                {
                    var di = new DirectoryInfo(dir);
                    _items.Add(new StorageItemViewModel
                    {
                        Name = di.Name,
                        FullPath = di.FullName,
                        Icon = "\uE8B7",
                        Detail = $"Folder  |  modified {di.LastWriteTime:yyyy-MM-dd HH:mm}",
                        Size = "",
                        IsDirectory = true
                    });
                }

                foreach (var file in Directory.GetFiles(path))
                {
                    var fi = new FileInfo(file);
                    _items.Add(new StorageItemViewModel
                    {
                        Name = fi.Name,
                        FullPath = fi.FullName,
                        Icon = "\uE8A5",
                        Detail = $"{FormatBytes(fi.Length)}  |  modified {fi.LastWriteTime:yyyy-MM-dd HH:mm}",
                        Size = FormatBytes(fi.Length),
                        IsDirectory = false
                    });
                }
            }
            catch (Exception ex)
            {
                _items.Add(new StorageItemViewModel
                {
                    Name = "Error: " + ex.Message,
                    FullPath = "",
                    Icon = "\uE783",
                    Detail = "Could not read directory",
                    Size = ""
                });
            }

            FileList.ItemsSource = _items;
        }

        private void UpdateUpButton()
        {
            var parent = Path.GetDirectoryName(_currentPath);
            UpButton.Visibility = (parent != null && _currentPath != _localRoot
                && parent.StartsWith(_localRoot, StringComparison.OrdinalIgnoreCase))
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnFileItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StorageItemViewModel item && item.IsDirectory)
            {
                _currentPath = item.FullPath;
                PathLabel.Text = _currentPath;
                UpdateUpButton();
                LoadDirectory(_currentPath);
            }
        }

        private async void OnDeleteItemClick(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || !(btn.CommandParameter is string path) || string.IsNullOrEmpty(path))
                return;

            var name = Path.GetFileName(path);
            var dlg = new ContentDialog
            {
                Title = "Delete?",
                Content = $"Delete \"{name}\"?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel"
            };

            if (await dlg.ShowAsync() == ContentDialogResult.Primary)
            {
                try
                {
                    if (Directory.Exists(path))
                        Directory.Delete(path, recursive: true);
                    else if (File.Exists(path))
                        File.Delete(path);

                    LoadDirectory(_currentPath);
                }
                catch (Exception ex)
                {
                    var err = new ContentDialog
                    {
                        Title = "Delete failed",
                        Content = ex.Message,
                        CloseButtonText = "OK"
                    };
                    await err.ShowAsync();
                }
            }
        }

        private void OnBackClick(object sender, RoutedEventArgs e) => App.Navigation.GoBack();

        private void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            LoadDirectory(_currentPath);
        }

        private void OnUpClick(object sender, RoutedEventArgs e)
        {
            var parent = Path.GetDirectoryName(_currentPath);
            if (parent != null && parent.StartsWith(_localRoot, StringComparison.OrdinalIgnoreCase))
            {
                _currentPath = parent;
                PathLabel.Text = _currentPath;
                UpdateUpButton();
                LoadDirectory(_currentPath);
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }
    }
}
