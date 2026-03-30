using System;
using System.Collections.Generic;

namespace PocketTavern.UWP.Models
{
    public class Chat
    {
        public string FileName { get; set; } = "";
        public string CharacterName { get; set; } = "";
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        public DateTimeOffset CreateDate { get; set; } = DateTimeOffset.Now;
    }

    public class ChatInfo
    {
        public string FileName { get; set; } = "";
        public string LastMessage { get; set; }
        public int MessageCount { get; set; } = 0;
        public long LastModified { get; set; } = 0;
    }
}
