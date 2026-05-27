namespace FaceEmotionApp.Services
{
    /// <summary>
    /// Oturum açan kullanıcının bilgilerini uygulama boyunca taşıyan singleton servis.
    /// </summary>
    public class UserSession
    {
        private static UserSession? _instance;
        public static UserSession Current => _instance ??= new UserSession();

        public string PersonName { get; set; } = "";
        public string Role { get; set; } = "";
        public List<string> AllowedRooms { get; set; } = new();

        public bool IsLoggedIn => !string.IsNullOrEmpty(PersonName);

        public void SetUser(string personName, string role, List<string> allowedRooms)
        {
            PersonName = personName;
            Role = role;
            AllowedRooms = allowedRooms;
        }

        public void Clear()
        {
            PersonName = "";
            Role = "";
            AllowedRooms = new();
        }
    }
}
