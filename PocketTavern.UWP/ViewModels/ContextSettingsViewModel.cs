namespace PocketTavern.UWP.ViewModels
{
    public class ContextSettingsViewModel : ViewModelBase
    {
        private string _authorsNoteContent = "";
        private int _authorsNoteInterval = 1;
        private int _authorsNoteDepth = 4;
        private int _authorsNotePosition = 0;
        private int _authorsNoteRole = 0;

        private bool _autoContinueEnabled = false;
        private int _autoContinueMinLength = 200;

        public string AuthorsNoteContent   { get => _authorsNoteContent;   set => Set(ref _authorsNoteContent,   value); }
        public int    AuthorsNoteInterval  { get => _authorsNoteInterval;  set => Set(ref _authorsNoteInterval,  value); }
        public int    AuthorsNoteDepth     { get => _authorsNoteDepth;     set => Set(ref _authorsNoteDepth,     value); }
        public int    AuthorsNotePosition  { get => _authorsNotePosition;  set => Set(ref _authorsNotePosition,  value); }
        public int    AuthorsNoteRole      { get => _authorsNoteRole;      set => Set(ref _authorsNoteRole,      value); }

        public bool   AutoContinueEnabled   { get => _autoContinueEnabled;   set => Set(ref _autoContinueEnabled,   value); }
        public int    AutoContinueMinLength { get => _autoContinueMinLength; set => Set(ref _autoContinueMinLength, value); }

        public void Load()
        {
            AuthorsNoteContent  = App.Settings.GetGlobalAuthorsNoteContent();
            AuthorsNoteInterval = App.Settings.GetGlobalAuthorsNoteInterval();
            AuthorsNoteDepth    = App.Settings.GetGlobalAuthorsNoteDepth();
            AuthorsNotePosition = App.Settings.GetGlobalAuthorsNotePosition();
            AuthorsNoteRole     = App.Settings.GetGlobalAuthorsNoteRole();

            AutoContinueEnabled   = App.Settings.GetAutoContinueEnabled();
            AutoContinueMinLength = App.Settings.GetAutoContinueMinLength();
        }

        public void Save()
        {
            App.Settings.SaveGlobalAuthorsNote(
                AuthorsNoteContent, AuthorsNoteDepth, AuthorsNoteInterval,
                AuthorsNotePosition, AuthorsNoteRole);
            App.Settings.SaveAutoContinueConfig(AutoContinueEnabled, AutoContinueMinLength);
            App.Navigation.GoBack();
        }
    }
}
