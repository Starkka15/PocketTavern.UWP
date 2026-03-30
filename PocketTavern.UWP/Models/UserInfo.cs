namespace PocketTavern.UWP.Models
{
    public class UserInfo
    {
        public string Handle { get; set; } = "";
        public string Name { get; set; } = "";
        public string Avatar { get; set; }
        public bool IsAdmin { get; set; } = false;
        public bool HasPassword { get; set; } = false;
        public long? Created { get; set; }
    }
}
