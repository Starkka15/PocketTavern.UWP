using SQLite;
using System;
using System.IO;
using Windows.Storage;

namespace PocketTavern.UWP.Data
{
    // SQLite entity classes

    [Table("characters")]
    public class CharacterEntity
    {
        [PrimaryKey]
        public string FileName { get; set; }
        public string Name { get; set; }
        public string Tags { get; set; }            // JSON array
        public bool IsFavorite { get; set; }
        public long LastChatDate { get; set; }
        public bool HasCharacterBook { get; set; }
        public bool UseAvatarForImageGen { get; set; } = true;
    }

    [Table("chats")]
    public class ChatEntity
    {
        [PrimaryKey]
        public string FileName { get; set; }
        public string CharacterName { get; set; }
        public long CreateDate { get; set; }
        public long ModifyDate { get; set; }
        public int MessageCount { get; set; }
    }

    public static class DatabaseHelper
    {
        private static SQLiteConnection _db;

        public static SQLiteConnection Db
        {
            get
            {
                if (_db == null)
                    throw new InvalidOperationException("Database not initialized. Call Initialize() first.");
                return _db;
            }
        }

        public static void Initialize()
        {
            SQLitePCL.Batteries.Init();
            string dbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "pockettavern.db");
            _db = new SQLiteConnection(dbPath);
            _db.CreateTable<CharacterEntity>();
            _db.CreateTable<ChatEntity>();
        }
    }
}
