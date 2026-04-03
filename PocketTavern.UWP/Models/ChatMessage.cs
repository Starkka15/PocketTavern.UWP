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
        public List<string> Alternates { get; set; } = new List<string>();
        public int CurrentSwipeIndex { get; set; } = 0;

        public int SwipeCount => Alternates?.Count ?? 0;
        public bool HasPrevSwipe => CurrentSwipeIndex > 0;
        public bool HasNextSwipe => Alternates != null && CurrentSwipeIndex < Alternates.Count - 1;

        public void AddAlternate(string text)
        {
            if (Alternates == null) Alternates = new List<string>();
            if (CurrentSwipeIndex < Alternates.Count)
                Alternates[CurrentSwipeIndex] = Content;
            else
                Alternates.Add(Content);
            Alternates.Add(text);
            CurrentSwipeIndex = Alternates.Count - 1;
            Content = text;
        }

        public void StoreCurrentAsAlternate()
        {
            if (Alternates == null) Alternates = new List<string>();
            if (Alternates.Count == 0)
            {
                Alternates.Add(Content);
            }
        }

        public string SwipeLeft()
        {
            if (!HasPrevSwipe) return Content;
            Alternates[CurrentSwipeIndex] = Content;
            CurrentSwipeIndex--;
            Content = Alternates[CurrentSwipeIndex];
            return Content;
        }

        public string SwipeRight()
        {
            if (!HasNextSwipe) return Content;
            Alternates[CurrentSwipeIndex] = Content;
            CurrentSwipeIndex++;
            Content = Alternates[CurrentSwipeIndex];
            return Content;
        }
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
