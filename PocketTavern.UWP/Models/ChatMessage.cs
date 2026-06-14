using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

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

    public class ChatMessage : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public string Id { get; set; } = Guid.NewGuid().ToString();

        private string _content = "";
        public string Content
        {
            get => _content;
            set { if (_content != value) { _content = value; Notify(nameof(Content)); } }
        }
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

        public bool HasHeaders => ExtensionHeaders != null && ExtensionHeaders.Count > 0
            && ExtensionHeaders.Exists(h => !string.IsNullOrEmpty(h.Text));
        public string HeaderText => ExtensionHeaders == null ? ""
            : string.Join("  ·  ", ExtensionHeaders.Where(h => !string.IsNullOrEmpty(h.Text)).Select(h => h.Text));

        public bool HasImage => !string.IsNullOrEmpty(ImagePath);
        public Uri ImageUri => HasImage
            ? new Uri("file:///" + System.IO.Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, ImagePath).Replace('\\', '/'))
            : null;

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
