using System.Text.RegularExpressions;

namespace PocketTavern.UWP.Services
{
    public static class TtsTextFilter
    {
        public static string Filter(string text, string mode)
        {
            if (string.IsNullOrEmpty(text)) return "";
            switch (mode)
            {
                case "quotes_only": return ExtractQuotes(text);
                case "no_asterisks": return RemoveAsterisks(text);
                default: return CleanForSpeech(text);
            }
        }

        /// <summary>Extract only text between double quotes.</summary>
        private static string ExtractQuotes(string text)
        {
            var matches = Regex.Matches(text, "\"([^\"]+)\"");
            var parts = new System.Collections.Generic.List<string>();
            foreach (Match m in matches)
                parts.Add(m.Groups[1].Value);
            return string.Join(" ", parts);
        }

        /// <summary>Remove action text wrapped in asterisks (*action*).</summary>
        private static string RemoveAsterisks(string text)
        {
            return CollapseWhitespace(Regex.Replace(text, @"\*[^*]+\*", ""));
        }

        /// <summary>Basic cleanup: strip markdown artifacts, collapse whitespace.</summary>
        private static string CleanForSpeech(string text)
        {
            var result = text;
            result = Regex.Replace(result, @"\*\*([^*]+)\*\*", "$1");  // **bold** → bold
            result = Regex.Replace(result, @"\*([^*]+)\*", "$1");       // *italic* → italic
            result = Regex.Replace(result, @"`[^`]+`", "");             // strip inline code
            return CollapseWhitespace(result);
        }

        private static string CollapseWhitespace(string text)
            => Regex.Replace(text, @"\s+", " ").Trim();
    }
}
