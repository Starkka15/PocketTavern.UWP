namespace PocketTavern.UWP.Models
{
    public abstract class StreamEvent
    {
        public class Token : StreamEvent
        {
            public string TokenText { get; set; }
            public string Accumulated { get; set; }
        }
        public class Complete : StreamEvent
        {
            public string FullText { get; set; }
        }
        public class Error : StreamEvent
        {
            public string Message { get; set; }
        }
    }

    public abstract class GroupStreamEvent
    {
        public class CharacterStarted : GroupStreamEvent
        {
            public string CharacterName { get; set; }
            public string CharacterAvatar { get; set; }
        }
        public class Token : GroupStreamEvent
        {
            public string TokenText { get; set; }
            public string Accumulated { get; set; }
        }
        public class CharacterComplete : GroupStreamEvent
        {
            public string CharacterName { get; set; }
            public string CharacterAvatar { get; set; }
            public string FullText { get; set; }
        }
        public class AllComplete : GroupStreamEvent { }
        public class Error : GroupStreamEvent
        {
            public string Message { get; set; }
        }
    }
}
