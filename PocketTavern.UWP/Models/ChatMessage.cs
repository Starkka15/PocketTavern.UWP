using System;
using System.Collections.Generic;

namespace PocketTavern.UWP.Models
{
    public class MessageHeaderEntry
    {
        public string Text { get; set; } = "";
        public string ExtensionId { get; set; } = "";
        public string CollapsibleText { get; set; } = "";
    }

    public class ChatMessageMetadata
    {
        public string NotePrompt { get; set; }
        public int? NoteInterval { get; set; }
        public int? NoteDepth { get; set; }
        public int? NotePosition { get; set; }
        public int? NoteRole { get; set; }
    }

    public class ChatMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Content { get; set; } = "";
        public bool IsUser { get; set; }
        public bool IsNarrator { get; set; } = false;
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
        public string IntegritySlug { get; set; }
        public ChatMessageMetadata ChatMetadata { get; set; }
        public string SenderName { get; set; }
        public string RawContent { get; set; }
        public List<MessageHeaderEntry> ExtensionHeaders { get; set; } = new List<MessageHeaderEntry>();
        public string ImagePath { get; set; }
    }

    public class GroupChatMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Content { get; set; } = "";
        public bool IsUser { get; set; }
        public bool IsSystem { get; set; } = false;
        public string SenderName { get; set; }
        public string SenderAvatar { get; set; }
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    }
}
