namespace PocketTavern.UWP.Models
{
    public enum PersonaPosition
    {
        InPrompt = 0,
        InChat = 1,
        TopOfAN = 2,
        BottomOfAN = 3
    }

    public enum PersonaRole
    {
        System = 0,
        User = 1,
        Assistant = 2
    }

    public class Persona
    {
        public string AvatarId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public PersonaPosition Position { get; set; } = PersonaPosition.InPrompt;
        public PersonaRole Role { get; set; } = PersonaRole.System;
        public int Depth { get; set; } = 2;
        public string Lorebook { get; set; } = "";
        public bool IsSelected { get; set; } = false;
    }
}
