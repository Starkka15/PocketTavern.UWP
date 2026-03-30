namespace PocketTavern.UWP.ViewModels
{
    public class PersonaViewModel : ViewModelBase
    {
        private string _name = "User";
        private string _description = "";
        private int _position = 0;
        private int _depth = 2;
        private int _role = 0;

        public string Name        { get => _name;        set => Set(ref _name,        value); }
        public string Description { get => _description; set => Set(ref _description, value); }
        public int Position       { get => _position;    set => Set(ref _position,    value); }
        public int Depth          { get => _depth;       set => Set(ref _depth,       value); }
        public int Role           { get => _role;        set => Set(ref _role,        value); }

        public void Load()
        {
            Name        = App.Settings.GetUserPersonaName();
            Description = App.Settings.GetUserPersonaDesc();
            Position    = App.Settings.GetUserPersonaPosition();
            Depth       = App.Settings.GetUserPersonaDepth();
            Role        = App.Settings.GetUserPersonaRole();
        }

        public void Save()
        {
            App.Settings.SaveUserPersonaName(Name);
            App.Settings.SaveUserPersonaDesc(Description);
            App.Settings.SaveUserPersonaPosition(Position);
            App.Settings.SaveUserPersonaDepth(Depth);
            App.Settings.SaveUserPersonaRole(Role);
            App.Navigation.GoBack();
        }
    }
}
