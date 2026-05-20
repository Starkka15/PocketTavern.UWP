using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using PocketTavern.UWP.Data;

namespace PocketTavern.UWP.ViewModels
{
    public class BackupExportViewModel : ViewModelBase
    {
        private bool _isExporting;
        private string _status = "";
        private int _charCount;
        private int _lorebookCount;
        private int _chatCount;

        public bool IsExporting { get => _isExporting; set => Set(ref _isExporting, value); }
        public string Status { get => _status; set => Set(ref _status, value); }
        public int CharCount { get => _charCount; set => Set(ref _charCount, value); }
        public int LorebookCount { get => _lorebookCount; set => Set(ref _lorebookCount, value); }
        public int ChatCount { get => _chatCount; set => Set(ref _chatCount, value); }
        public bool IsComplete { get; private set; }

        public async Task ExportAsync()
        {
            IsExporting = true;
            IsComplete = false;
            Status = "Preparing…";
            CharCount = 0; LorebookCount = 0; ChatCount = 0;

            try
            {
                var picker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    SuggestedFileName = $"PocketTavern_backup_{DateTime.Now:yyyyMMdd_HHmmss}"
                };
                picker.FileTypeChoices.Add("ZIP archive", new List<string> { ".zip" });

                var outFile = await picker.PickSaveFileAsync();
                if (outFile == null) { IsExporting = false; return; }

                Status = "Building archive…";

                var localPath = ApplicationData.Current.LocalFolder.Path;
                var charsDir = Path.Combine(localPath, "characters");
                var avatarsDir = Path.Combine(localPath, "characters", "avatars");
                var worldsDir = Path.Combine(localPath, "worlds");
                var chatsDir = Path.Combine(localPath, "chats");

                using (var ms = new MemoryStream())
                {
                    using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
                    {
                        // Characters — prefer PNG (original ST card); fall back to JSON
                        if (Directory.Exists(charsDir))
                        {
                            var jsonFiles = Directory.GetFiles(charsDir, "*.json");
                            foreach (var jsonFile in jsonFiles)
                            {
                                var stem = Path.GetFileNameWithoutExtension(jsonFile);
                                var pngPath = Path.Combine(avatarsDir, stem + ".png");
                                if (File.Exists(pngPath))
                                {
                                    zip.CreateEntryFromBytes($"characters/{stem}.png",
                                        File.ReadAllBytes(pngPath));
                                }
                                else
                                {
                                    zip.CreateEntryFromBytes($"characters/{stem}.json",
                                        File.ReadAllBytes(jsonFile));
                                }
                                CharCount++;
                            }
                        }

                        // Lorebooks
                        if (Directory.Exists(worldsDir))
                        {
                            foreach (var f in Directory.GetFiles(worldsDir, "*.json"))
                            {
                                zip.CreateEntryFromBytes($"worlds/{Path.GetFileName(f)}",
                                    File.ReadAllBytes(f));
                                LorebookCount++;
                            }
                        }

                        // Chats
                        if (Directory.Exists(chatsDir))
                        {
                            foreach (var charDir in Directory.GetDirectories(chatsDir))
                            {
                                // Skip group chat folders
                                var dirName = Path.GetFileName(charDir);
                                if (dirName.StartsWith("__group__")) continue;

                                foreach (var chatFile in Directory.GetFiles(charDir, "*.jsonl")
                                    .Concat(Directory.GetFiles(charDir, "*.json")))
                                {
                                    zip.CreateEntryFromBytes(
                                        $"chats/{dirName}/{Path.GetFileName(chatFile)}",
                                        File.ReadAllBytes(chatFile));
                                    ChatCount++;
                                }
                            }
                        }
                    }

                    ms.Seek(0, SeekOrigin.Begin);
                    await FileIO.WriteBytesAsync(outFile, ms.ToArray());
                }

                Status = $"Done — {CharCount} characters, {LorebookCount} lorebooks, {ChatCount} chats";
                IsComplete = true;
            }
            catch (Exception ex)
            {
                Status = $"Error: {ex.Message}";
            }
            finally
            {
                IsExporting = false;
            }
        }
    }

    internal static class ZipArchiveExtensions
    {
        public static void CreateEntryFromBytes(this ZipArchive zip, string entryName, byte[] bytes)
        {
            var entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);
            using (var stream = entry.Open())
                stream.Write(bytes, 0, bytes.Length);
        }
    }
}
