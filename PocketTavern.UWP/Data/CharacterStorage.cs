using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.Data
{
    /// <summary>
    /// Stores character data as JSON files in LocalFolder\characters\.
    /// Character PNG cards are stored in LocalFolder\characters\avatars\.
    /// </summary>
    public class CharacterStorage
    {
        private readonly string _charsDir;
        private readonly string _avatarsDir;

        public CharacterStorage()
        {
            var localPath = ApplicationData.Current.LocalFolder.Path;
            _charsDir = Path.Combine(localPath, "characters");
            _avatarsDir = Path.Combine(localPath, "characters", "avatars");
            Directory.CreateDirectory(_charsDir);
            Directory.CreateDirectory(_avatarsDir);
        }

        /// <summary>Returns all characters sorted by name.</summary>
        public async Task<List<Character>> GetAllCharactersAsync()
        {
            var result = new List<Character>();
            foreach (var file in Directory.GetFiles(_charsDir, "*.json"))
            {
                try
                {
                    var json = await ReadFileAsync(file);
                    var ch = ParseCharacterJson(json);
                    if (ch != null)
                    {
                        // Ensure Avatar is always set to the file stem so navigation/avatar lookup works
                        if (string.IsNullOrEmpty(ch.Avatar))
                            ch.Avatar = Path.GetFileNameWithoutExtension(file);
                        result.Add(ch);
                    }
                }
                catch { }
            }
            return result.OrderBy(c => c.Name).ToList();
        }

        public async Task<Character> GetCharacterAsync(string fileName)
        {
            var path = Path.Combine(_charsDir, fileName + ".json");
            if (!File.Exists(path)) return null;
            var json = await ReadFileAsync(path);
            var ch = ParseCharacterJson(json);
            if (ch != null && string.IsNullOrEmpty(ch.Avatar))
                ch.Avatar = fileName;
            return ch;
        }

        public async Task SaveCharacterAsync(string fileName, Character character)
        {
            var json = CharacterToJson(character);
            var path = Path.Combine(_charsDir, fileName + ".json");
            await WriteFileAsync(path, json);
            UpsertEntity(fileName, character);
        }

        public async Task DeleteCharacterAsync(string fileName)
        {
            var jsonPath = Path.Combine(_charsDir, fileName + ".json");
            if (File.Exists(jsonPath)) File.Delete(jsonPath);

            var avatarPath = Path.Combine(_avatarsDir, fileName + ".png");
            if (File.Exists(avatarPath)) File.Delete(avatarPath);

            try { DatabaseHelper.Db.Delete<CharacterEntity>(fileName); } catch { }
        }

        public string GetAvatarPath(string fileName) =>
            Path.Combine(_avatarsDir, fileName + ".png");

        public bool AvatarExists(string fileName) =>
            File.Exists(Path.Combine(_avatarsDir, fileName + ".png"));

        /// <summary>Copies an image file picked by the user into the avatars folder.</summary>
        public async Task CopyAvatarAsync(string sourcePath, string destFileName)
        {
            var destPath = Path.Combine(_avatarsDir, destFileName);
            var sourceFile = await StorageFile.GetFileFromPathAsync(sourcePath);
            var destFolder = await StorageFolder.GetFolderFromPathAsync(_avatarsDir);
            await sourceFile.CopyAsync(destFolder, destFileName, NameCollisionOption.ReplaceExisting);
        }

        // ── DB sync ──────────────────────────────────────────────────────────────

        private void UpsertEntity(string fileName, Character ch)
        {
            try
            {
                var entity = new CharacterEntity
                {
                    FileName = fileName,
                    Name = ch.Name,
                    Tags = JsonConvert.SerializeObject(ch.Tags),
                    IsFavorite = ch.IsFavorite,
                    LastChatDate = 0,
                    HasCharacterBook = ch.HasCharacterBook,
                    UseAvatarForImageGen = ch.UseAvatarForImageGen
                };
                DatabaseHelper.Db.InsertOrReplace(entity);
            }
            catch { }
        }

        // ── JSON serialization — SillyTavern V2 card format ─────────────────────

        private Character ParseCharacterJson(string json)
        {
            var obj = JObject.Parse(json);
            // Support both flat and V2 nested formats
            var data = obj["data"] as JObject ?? obj;

            return new Character
            {
                Name = (string)data["name"] ?? "",
                Avatar = (string)data["avatar"],
                Description = (string)data["description"] ?? "",
                Personality = (string)data["personality"] ?? "",
                Scenario = (string)data["scenario"] ?? "",
                FirstMessage = (string)(data["first_mes"] ?? data["firstMessage"]) ?? "",
                MessageExample = (string)(data["mes_example"] ?? data["messageExample"]) ?? "",
                CreatorNotes = (string)(data["creator_notes"] ?? data["creatorNotes"]) ?? "",
                SystemPrompt = (string)(data["system_prompt"] ?? data["systemPrompt"]) ?? "",
                Tags = data["tags"]?.ToObject<List<string>>() ?? new List<string>(),
                AlternateGreetings = data["alternate_greetings"]?.ToObject<List<string>>() ?? new List<string>(),
                PostHistoryInstructions = (string)(data["post_history_instructions"] ?? data["postHistoryInstructions"]) ?? "",
                DepthPrompt = (string)(data["depth_prompt"] ?? data["depthPrompt"]) ?? "",
                DepthPromptDepth = (int?)data["depth_prompt_depth"] ?? 4,
                DepthPromptRole = (string)data["depth_prompt_role"] ?? "system",
                Talkativeness = (float?)data["talkativeness"] ?? 0.5f,
                IsFavorite = (bool?)data["fav"] ?? false,
                UseAvatarForImageGen = (bool?)data["use_avatar_for_image_gen"] ?? true,
                HasCharacterBook = data["character_book"] != null
            };
        }

        private string CharacterToJson(Character ch)
        {
            var data = new JObject
            {
                ["spec"] = "chara_card_v2",
                ["spec_version"] = "2.0",
                ["data"] = new JObject
                {
                    ["name"] = ch.Name,
                    ["avatar"] = ch.Avatar,
                    ["description"] = ch.Description,
                    ["personality"] = ch.Personality,
                    ["scenario"] = ch.Scenario,
                    ["first_mes"] = ch.FirstMessage,
                    ["mes_example"] = ch.MessageExample,
                    ["creator_notes"] = ch.CreatorNotes,
                    ["system_prompt"] = ch.SystemPrompt,
                    ["tags"] = JArray.FromObject(ch.Tags ?? new List<string>()),
                    ["alternate_greetings"] = JArray.FromObject(ch.AlternateGreetings ?? new List<string>()),
                    ["post_history_instructions"] = ch.PostHistoryInstructions,
                    ["depth_prompt"] = ch.DepthPrompt,
                    ["depth_prompt_depth"] = ch.DepthPromptDepth,
                    ["depth_prompt_role"] = ch.DepthPromptRole,
                    ["talkativeness"] = ch.Talkativeness,
                    ["fav"] = ch.IsFavorite,
                    ["use_avatar_for_image_gen"] = ch.UseAvatarForImageGen
                }
            };
            return data.ToString(Formatting.Indented);
        }

        // ── PNG card import ──────────────────────────────────────────────────────

        /// <summary>
        /// Saves a PNG character card (bytes) plus a basic JSON stub.
        /// The avatar is stored; a best-effort JSON is written using the provided name.
        /// </summary>
        public async Task ImportCharacterFromBytesAsync(string displayName, byte[] pngBytes)
        {
            if (string.IsNullOrWhiteSpace(displayName) || pngBytes == null || pngBytes.Length == 0)
                return;

            // Derive a safe file name
            var fileName = MakeSafeFileName(displayName);

            // Save PNG avatar
            var avatarPath = Path.Combine(_avatarsDir, fileName + ".png");
            await WriteBytesAsync(avatarPath, pngBytes);

            // Try to extract V2 JSON from PNG tEXt chunk (keyword "chara")
            Character character = null;
            try { character = ExtractCharaFromPng(pngBytes); } catch { }

            if (character == null)
                character = new Character { Name = displayName };
            if (string.IsNullOrEmpty(character.Name))
                character.Name = displayName;
            character.Avatar = fileName;

            await SaveCharacterAsync(fileName, character);
        }

        private Character ExtractCharaFromPng(byte[] png)
        {
            // Walk PNG chunks looking for tEXt or iTXt with keyword "chara"
            int pos = 8; // skip PNG signature
            while (pos + 12 <= png.Length)
            {
                int length = ReadInt32Be(png, pos);
                string type = System.Text.Encoding.ASCII.GetString(png, pos + 4, 4);

                if (pos + 8 + length <= png.Length)
                {
                    if (type == "tEXt")
                    {
                        var data = new byte[length];
                        Array.Copy(png, pos + 8, data, 0, length);
                        var text = System.Text.Encoding.GetEncoding(28591).GetString(data);
                        var sep = text.IndexOf('\0');
                        if (sep >= 0 && text.Substring(0, sep) == "chara")
                        {
                            var b64 = text.Substring(sep + 1);
                            var jsonBytes = Convert.FromBase64String(b64);
                            var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
                            return ParseCharacterJson(json);
                        }
                    }
                    else if (type == "iTXt")
                    {
                        // iTXt: keyword\0 compression_flag(1) compression_method(1) language\0 translated_keyword\0 text
                        var chunkStart = pos + 8;
                        var chunkEnd = chunkStart + length;

                        // Read keyword (null-terminated)
                        var kwEnd = Array.IndexOf(png, (byte)0, chunkStart, length);
                        if (kwEnd < 0) { pos += 12 + length; continue; }
                        var keyword = System.Text.Encoding.ASCII.GetString(png, chunkStart, kwEnd - chunkStart);
                        if (keyword != "chara") { pos += 12 + length; continue; }

                        var cursor = kwEnd + 1;
                        if (cursor + 2 > chunkEnd) { pos += 12 + length; continue; }
                        var compressionFlag   = png[cursor];
                        var compressionMethod = png[cursor + 1];
                        cursor += 2;

                        // Skip language tag (null-terminated)
                        var langEnd = Array.IndexOf(png, (byte)0, cursor, chunkEnd - cursor);
                        if (langEnd < 0) { pos += 12 + length; continue; }
                        cursor = langEnd + 1;

                        // Skip translated keyword (null-terminated)
                        var tkEnd = Array.IndexOf(png, (byte)0, cursor, chunkEnd - cursor);
                        if (tkEnd < 0) { pos += 12 + length; continue; }
                        cursor = tkEnd + 1;

                        // Remaining bytes = text
                        var textLen = chunkEnd - cursor;
                        if (textLen <= 0) { pos += 12 + length; continue; }
                        var textBytes = new byte[textLen];
                        Array.Copy(png, cursor, textBytes, 0, textLen);

                        string b64Text;
                        if (compressionFlag == 1 && compressionMethod == 0)
                        {
                            // zlib-compressed — decompress with DeflateStream (skip 2-byte zlib header)
                            using (var ms = new MemoryStream(textBytes, 2, textBytes.Length - 2))
                            using (var ds = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionMode.Decompress))
                            using (var sr = new StreamReader(ds, System.Text.Encoding.UTF8))
                                b64Text = sr.ReadToEnd();
                        }
                        else
                        {
                            b64Text = System.Text.Encoding.UTF8.GetString(textBytes);
                        }

                        b64Text = b64Text.Trim();
                        var jsonBytes2 = Convert.FromBase64String(b64Text);
                        var json = System.Text.Encoding.UTF8.GetString(jsonBytes2);
                        return ParseCharacterJson(json);
                    }
                }

                pos += 12 + length; // length(4) + type(4) + data + crc(4)
            }
            return null;
        }

        private static int ReadInt32Be(byte[] data, int offset)
            => (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];

        private static string MakeSafeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new System.Text.StringBuilder();
            foreach (var c in name)
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            return sb.ToString().Trim('_', ' ').Replace(' ', '_');
        }

        private static async Task WriteBytesAsync(string path, byte[] bytes)
        {
            var dir = Path.GetDirectoryName(path);
            var name = Path.GetFileName(path);
            var folder = await StorageFolder.GetFolderFromPathAsync(dir);
            var file = await folder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteBytesAsync(file, bytes);
        }

        // ── File I/O helpers ─────────────────────────────────────────────────────

        private static async Task<string> ReadFileAsync(string path)
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            return await FileIO.ReadTextAsync(file);
        }

        private static async Task WriteFileAsync(string path, string content)
        {
            var dir = Path.GetDirectoryName(path);
            var name = Path.GetFileName(path);
            var folder = await StorageFolder.GetFolderFromPathAsync(dir);
            var file = await folder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, content);
        }
    }
}
