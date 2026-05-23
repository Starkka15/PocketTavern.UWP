using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace PocketTavern.UWP.Data
{
    /// <summary>
    /// Stores expression sprites at LocalFolder\characters\sprites\{characterName}\{spriteName}.png.
    /// Lookup is case-insensitive and strips .png suffix from keys. (V7)
    /// </summary>
    public class SpriteStorage
    {
        private readonly string _spritesRoot;

        public SpriteStorage()
        {
            _spritesRoot = Path.Combine(ApplicationData.Current.LocalFolder.Path, "characters", "sprites");
            Directory.CreateDirectory(_spritesRoot);
        }

        /// <summary>Saves all sprites for a character, overwriting any existing files.</summary>
        public async Task SaveAsync(string characterName, Dictionary<string, byte[]> sprites)
        {
            if (string.IsNullOrWhiteSpace(characterName) || sprites == null || sprites.Count == 0)
                return;

            var dir = GetSpriteDir(characterName);
            Directory.CreateDirectory(dir);

            var folder = await StorageFolder.GetFolderFromPathAsync(dir);
            foreach (var kv in sprites)
            {
                var fileName = StripPngSuffix(kv.Key) + ".png";
                var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteBytesAsync(file, kv.Value);
            }
        }

        /// <summary>
        /// Returns the StorageFile for a sprite, or null if not found.
        /// Case-insensitive; strips .png suffix from spriteName. (V7)
        /// </summary>
        public StorageFile GetFile(string characterName, string spriteName)
        {
            if (string.IsNullOrWhiteSpace(characterName) || string.IsNullOrWhiteSpace(spriteName))
                return null;

            var dir = GetSpriteDir(characterName);
            if (!Directory.Exists(dir)) return null;

            var key = StripPngSuffix(spriteName).ToLowerInvariant();
            var files = Directory.GetFiles(dir, "*.png");

            // 1. Exact match (case-insensitive)
            foreach (var path in files)
            {
                if (string.Equals(Path.GetFileNameWithoutExtension(path), key, StringComparison.OrdinalIgnoreCase))
                {
                    try { return StorageFile.GetFileFromPathAsync(path).AsTask().Result; }
                    catch { return null; }
                }
            }

            // 2. Fuzzy: sprite filename is a prefix of the requested name (e.g. "happy" matches "happy cheerful")
            // or requested name starts with the sprite filename
            foreach (var path in files)
            {
                var stem = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (key.StartsWith(stem) || stem.StartsWith(key))
                {
                    try { return StorageFile.GetFileFromPathAsync(path).AsTask().Result; }
                    catch { return null; }
                }
            }

            return null;
        }

        /// <summary>Returns the list of sprite names (without .png extension) for a character.</summary>
        public IList<string> List(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName)) return new List<string>();
            var dir = GetSpriteDir(characterName);
            if (!Directory.Exists(dir)) return new List<string>();
            return Directory.GetFiles(dir, "*.png")
                .Select(p => Path.GetFileNameWithoutExtension(p))
                .OrderBy(n => n)
                .ToList();
        }

        private string GetSpriteDir(string characterName)
            => Path.Combine(_spritesRoot, characterName);

        private static string StripPngSuffix(string name)
            => name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - 4)
                : name;
    }
}
