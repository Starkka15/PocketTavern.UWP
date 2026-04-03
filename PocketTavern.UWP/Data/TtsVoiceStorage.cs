using Windows.Storage;

namespace PocketTavern.UWP.Data
{
    public static class TtsVoiceStorage
    {
        private static readonly ApplicationDataContainer _prefs =
            ApplicationData.Current.LocalSettings;

        private static string Key(string prefix, string characterFile)
            => $"tts_{prefix}_{Safe(characterFile)}";

        public static string GetVoiceId(string characterFile)
        {
            var val = _prefs.Values[Key("voice", characterFile)] as string;
            return string.IsNullOrEmpty(val) ? null : val;
        }

        public static void SetVoiceId(string characterFile, string voiceId)
        {
            _prefs.Values[Key("voice", characterFile)] = voiceId;
        }

        public static string GetProviderOverride(string characterFile)
        {
            var val = _prefs.Values[Key("provider", characterFile)] as string;
            return string.IsNullOrEmpty(val) ? null : val;
        }

        public static void SetProviderOverride(string characterFile, string provider)
        {
            if (!string.IsNullOrEmpty(provider))
                _prefs.Values[Key("provider", characterFile)] = provider;
            else
                _prefs.Values.Remove(Key("provider", characterFile));
        }

        public static void ClearVoice(string characterFile)
        {
            _prefs.Values.Remove(Key("voice", characterFile));
            _prefs.Values.Remove(Key("provider", characterFile));
        }

        private static string Safe(string name)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var sb = new System.Text.StringBuilder();
            foreach (var c in name)
                sb.Append(System.Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            return sb.ToString();
        }
    }
}
