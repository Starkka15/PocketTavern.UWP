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
    /// Stores chats as JSON files in LocalFolder\chats\{characterName}\.
    /// Each file is a JSON array of ChatMessage objects.
    /// </summary>
    public class ChatStorage
    {
        private readonly string _chatsDir;

        public ChatStorage()
        {
            _chatsDir = Path.Combine(ApplicationData.Current.LocalFolder.Path, "chats");
            Directory.CreateDirectory(_chatsDir);
        }

        public string GetChatDir(string characterName)
        {
            var dir = Path.Combine(_chatsDir, SanitizeName(characterName));
            Directory.CreateDirectory(dir);
            return dir;
        }

        public async Task<List<ChatInfo>> GetChatInfosAsync(string characterName)
        {
            var dir = GetChatDir(characterName);
            var result = new List<ChatInfo>();
            foreach (var file in Directory.GetFiles(dir, "*.jsonl").Concat(Directory.GetFiles(dir, "*.json")))
            {
                try
                {
                    var info = new FileInfo(file);
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var messages = await LoadMessagesAsync(file);
                    result.Add(new ChatInfo
                    {
                        FileName = fileName,
                        MessageCount = messages.Count,
                        LastMessage = messages.LastOrDefault(m => !m.IsUser)?.Content,
                        LastModified = info.LastWriteTimeUtc.ToFileTimeUtc()
                    });
                }
                catch { }
            }
            return result.OrderByDescending(c => c.LastModified).ToList();
        }

        public async Task<Chat> LoadChatAsync(string characterName, string fileName)
        {
            var dir = GetChatDir(characterName);
            var path = FindChatFile(dir, fileName);
            if (path == null) return null;

            var messages = await LoadMessagesAsync(path);
            var chat = new Chat
            {
                FileName = fileName,
                CharacterName = characterName,
                Messages = messages,
                CreateDate = DateTimeOffset.FromFileTime(new FileInfo(path).CreationTimeUtc.ToFileTimeUtc())
            };

            // Load companion memory file if present
            var memPath = Path.Combine(dir, fileName + ".memory.json");
            if (File.Exists(memPath))
            {
                try
                {
                    var memFile = await StorageFile.GetFileFromPathAsync(memPath);
                    var json = await FileIO.ReadTextAsync(memFile);
                    var obj = JObject.Parse(json);
                    chat.MemoryBlock = (string)obj["memory_block"] ?? "";
                    chat.SummarizedTurnCount = (int?)obj["summarized_turn_count"] ?? 0;
                }
                catch { }
            }

            return chat;
        }

        public async Task SaveChatMemoryAsync(string characterName, string fileName, string memoryBlock, int summarizedTurnCount)
        {
            var dir = GetChatDir(characterName);
            var memPath = Path.Combine(dir, fileName + ".memory.json");
            var obj = new JObject
            {
                ["memory_block"] = memoryBlock ?? "",
                ["summarized_turn_count"] = summarizedTurnCount
            };
            var folder = await StorageFolder.GetFolderFromPathAsync(dir);
            var file = await folder.CreateFileAsync(Path.GetFileName(memPath), CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, obj.ToString());
        }

        public async Task SaveChatAsync(string characterName, string fileName, List<ChatMessage> messages)
        {
            var dir = GetChatDir(characterName);
            var path = Path.Combine(dir, fileName + ".jsonl");
            var lines = messages.Select(m => JsonConvert.SerializeObject(MessageToJObject(m)));
            var folder = await StorageFolder.GetFolderFromPathAsync(dir);
            var file = await folder.CreateFileAsync(Path.GetFileName(path), CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteLinesAsync(file, lines);

            // Update DB record
            try
            {
                DatabaseHelper.Db.InsertOrReplace(new ChatEntity
                {
                    FileName = fileName,
                    CharacterName = characterName,
                    CreateDate = DateTimeOffset.Now.ToUnixTimeSeconds(),
                    ModifyDate = DateTimeOffset.Now.ToUnixTimeSeconds(),
                    MessageCount = messages.Count
                });
            }
            catch { }
        }

        public async Task AppendMessageAsync(string characterName, string fileName, ChatMessage message)
        {
            var dir = GetChatDir(characterName);
            var path = FindChatFile(dir, fileName);
            if (path == null)
                path = Path.Combine(dir, fileName + ".jsonl");

            var line = JsonConvert.SerializeObject(MessageToJObject(message));
            var isNew = !File.Exists(path);

            if (isNew)
            {
                var folder = await StorageFolder.GetFolderFromPathAsync(dir);
                var file = await folder.CreateFileAsync(fileName + ".jsonl", CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, line + "\n");
            }
            else
            {
                var folder = await StorageFolder.GetFolderFromPathAsync(dir);
                var file = await folder.GetFileAsync(Path.GetFileName(path));
                await FileIO.AppendTextAsync(file, line + "\n");
            }

            try
            {
                var existing = DatabaseHelper.Db.Get<ChatEntity>(fileName);
                if (existing != null)
                {
                    existing.MessageCount++;
                    existing.ModifyDate = DateTimeOffset.Now.ToUnixTimeSeconds();
                    DatabaseHelper.Db.Update(existing);
                }
                else if (isNew)
                {
                    DatabaseHelper.Db.InsertOrReplace(new ChatEntity
                    {
                        FileName = fileName,
                        CharacterName = characterName,
                        CreateDate = DateTimeOffset.Now.ToUnixTimeSeconds(),
                        ModifyDate = DateTimeOffset.Now.ToUnixTimeSeconds(),
                        MessageCount = 1
                    });
                }
            }
            catch { }
        }

        public async Task DeleteChatAsync(string characterName, string fileName)
        {
            var dir = GetChatDir(characterName);
            var path = FindChatFile(dir, fileName);
            if (path != null && File.Exists(path)) File.Delete(path);
            try { DatabaseHelper.Db.Delete<ChatEntity>(fileName); } catch { }
        }

        public string CreateChatFileName(string characterName) =>
            characterName + " - " + DateTime.UtcNow.ToString("yyyy-MM-ddTHH.mm.ss");

        public Task RenameChatAsync(string characterName, string oldFileName, string newFileName)
        {
            var dir = GetChatDir(characterName);
            var oldPath = FindChatFile(dir, oldFileName);
            if (oldPath == null) return Task.CompletedTask;
            var ext  = Path.GetExtension(oldPath);
            var safe = SanitizeName(newFileName);
            var newPath = Path.Combine(dir, safe + ext);
            if (!File.Exists(newPath))
                File.Move(oldPath, newPath);
            return Task.CompletedTask;
        }

        // ── Serialization ────────────────────────────────────────────────────────

        private async Task<List<ChatMessage>> LoadMessagesAsync(string path)
        {
            var messages = new List<ChatMessage>();
            var file = await StorageFile.GetFileFromPathAsync(path);
            var lines = await FileIO.ReadLinesAsync(file);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var obj = JObject.Parse(line);
                    // Skip ST-format header line (has user_name/character_name, no message content)
                    if (obj["user_name"] != null || obj["character_name"] != null) continue;
                    messages.Add(ParseMessageJObject(obj));
                }
                catch { }
            }
            return messages;
        }

        private ChatMessage ParseMessageJObject(JObject obj)
        {
            // ST compat: "mes" is the Android/SillyTavern field; "content" is native UWP
            var content = (string)obj["content"] ?? (string)obj["mes"] ?? "";

            // ST compat: "send_date" is Android format ("yyyy-MM-dd HH:mm:ss"); "timestamp" is native ISO 8601
            DateTimeOffset timestamp;
            if (obj["timestamp"] != null)
                timestamp = DateTimeOffset.Parse((string)obj["timestamp"]);
            else if (obj["send_date"] != null && DateTimeOffset.TryParse((string)obj["send_date"], out var sd))
                timestamp = sd;
            else
                timestamp = DateTimeOffset.Now;

            var isUser = (bool?)obj["is_user"] ?? false;

            return new ChatMessage
            {
                Id = (string)obj["id"] ?? Guid.NewGuid().ToString(),
                Content = content,
                IsUser = isUser,
                // ST compat: Android writes "is_system" for narrator lines
                IsNarrator = (bool?)obj["is_narrator"] ?? (bool?)obj["is_system"] ?? false,
                Timestamp = timestamp,
                // ST compat: "name" is the Android sender field; only use for AI messages
                SenderName = (string)obj["sender_name"] ?? (!isUser ? (string)obj["name"] : null),
                ImagePath = (string)obj["image_path"]
            };
        }

        private JObject MessageToJObject(ChatMessage m)
        {
            var obj = new JObject
            {
                ["id"] = m.Id,
                ["content"] = m.Content,
                ["is_user"] = m.IsUser,
                ["timestamp"] = m.Timestamp.ToString("o")
            };
            if (m.IsNarrator) obj["is_narrator"] = true;
            if (m.SenderName != null) obj["sender_name"] = m.SenderName;
            if (m.ImagePath != null) obj["image_path"] = m.ImagePath;
            return obj;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private string FindChatFile(string dir, string fileName)
        {
            var jsonl = Path.Combine(dir, fileName + ".jsonl");
            if (File.Exists(jsonl)) return jsonl;
            var json = Path.Combine(dir, fileName + ".json");
            if (File.Exists(json)) return json;
            return null;
        }

        private static string SanitizeName(string name) =>
            string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
    }
}
