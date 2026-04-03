using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace PocketTavern.UWP.Data
{
    public class BackgroundStorage
    {
        private readonly string _backgroundsDir;

        public BackgroundStorage()
        {
            var localPath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            _backgroundsDir = Path.Combine(localPath, "backgrounds");
            Directory.CreateDirectory(_backgroundsDir);
        }

        private string GetBackgroundPath(string characterId)
        {
            var safeId = MakeSafe(characterId);
            return Path.Combine(_backgroundsDir, safeId + ".png");
        }

        public bool HasBackground(string characterId)
            => File.Exists(GetBackgroundPath(characterId));

        public string GetBackgroundPathOrNull(string characterId)
        {
            var path = GetBackgroundPath(characterId);
            return File.Exists(path) ? path : null;
        }

        public async System.Threading.Tasks.Task<bool> SaveFromStorageFileAsync(string characterId, StorageFile file)
        {
            try
            {
                var destPath = GetBackgroundPath(characterId);
                var destFolder = await StorageFolder.GetFolderFromPathAsync(_backgroundsDir);
                await file.CopyAsync(destFolder, Path.GetFileName(destPath), NameCollisionOption.ReplaceExisting);
                return true;
            }
            catch { return false; }
        }

        public bool DeleteBackground(string characterId)
        {
            try
            {
                var path = GetBackgroundPath(characterId);
                if (File.Exists(path)) File.Delete(path);
                return true;
            }
            catch { return false; }
        }

        public BitmapImage LoadAsBitmap(string characterId)
        {
            var path = GetBackgroundPathOrNull(characterId);
            if (path == null) return null;
            try { return new BitmapImage(new Uri("file:///" + path.Replace('\\', '/'))); }
            catch { return null; }
        }

        private static string MakeSafe(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new System.Text.StringBuilder();
            foreach (var c in name)
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            return sb.ToString().Trim('_', ' ').Replace(' ', '_');
        }
    }
}
