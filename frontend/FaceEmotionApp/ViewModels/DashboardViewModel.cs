using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FaceEmotionApp.Services;

namespace FaceEmotionApp.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly ApiService _apiService;

        public DashboardViewModel()
        {
            _apiService = new ApiService();
        }

        // ================================================
        // ÖZELLİKLER
        // ================================================

        private string _welcomeText = "";
        public string WelcomeText
        {
            get => _welcomeText;
            set => SetProperty(ref _welcomeText, value);
        }

        private string _roleText = "";
        public string RoleText
        {
            get => _roleText;
            set => SetProperty(ref _roleText, value);
        }

        private List<RoomCardItem> _rooms = new();
        public List<RoomCardItem> Rooms
        {
            get => _rooms;
            set => SetProperty(ref _rooms, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        // ================================================
        // VERİ YÜKLEME
        // ================================================

        public async Task RefreshDataAsync()
        {
            IsBusy = true;
            try
            {
                var session = UserSession.Current;
                
                // 1. Rol bilgilerini ve izinleri güncelle (Sunucudan en güncelini çek)
                var roles = await _apiService.GetRolesAsync();
                var currentRoleData = roles.FirstOrDefault(r => r.Name == session.Role);
                if (currentRoleData != null)
                {
                    session.AllowedRooms = currentRoleData.Allowed_Rooms ?? new List<string>();
                }

                WelcomeText = $"Hoş geldiniz, {session.PersonName}!";
                RoleText = $"Rol: {session.Role}";
                OnPropertyChanged(nameof(IsAdmin));

                // 2. Tüm odaları çek
                var allRooms = await _apiService.GetRoomsAsync();

                // 3. Sadece kullanıcının rolünün erişebildiği odaları göster
                var allowedRooms = session.AllowedRooms;

                Rooms = allRooms
                    .Where(r => r.Name != null && allowedRooms.Contains(r.Name))
                    .Select(r => new RoomCardItem
                    {
                        RoomName = r.Name ?? "",
                        RequiredEmotion = r.Required_Emotion ?? "happy",
                        EmotionEmoji = GetEmotionEmoji(r.Required_Emotion ?? "happy"),
                        EmotionColor = GetEmotionColor(r.Required_Emotion ?? "happy")
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                await ShowAlert("Hata", $"Veriler güncellenemedi: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ================================================
        // KOMUTLAR
        // ================================================

        [RelayCommand]
        public async Task SelectRoomAsync(RoomCardItem room)
        {
            await Shell.Current.GoToAsync($"//RoomAccessPage?roomName={Uri.EscapeDataString(room.RoomName)}&requiredEmotion={Uri.EscapeDataString(room.RequiredEmotion)}");
        }

        [RelayCommand]
        public async Task GoToEmotionAnalysisAsync()
        {
            await Shell.Current.GoToAsync("//EmotionPage");
        }

        [RelayCommand]
        public async Task GoToAdminAsync()
        {
            await Shell.Current.GoToAsync("//AdminPage");
        }

        [RelayCommand]
        public async Task LogoutAsync()
        {
            UserSession.Current.Clear();
            await Shell.Current.GoToAsync("//MainPage");
        }

        // ================================================
        // ÖZELLİK: MÜDÜR KONTROLÜ
        // ================================================

        public bool IsAdmin => UserSession.Current.Role == "Müdür";

        // ================================================
        // YARDIMCILAR
        // ================================================

        private static string GetEmotionEmoji(string emotion) => emotion.ToLower() switch
        {
            "happy"    => "😊",
            "neutral"  => "😐",
            "sad"      => "😢",
            "angry"    => "😡",
            "fear"     => "😨",
            "surprise" => "😲",
            "disgust"  => "🤢",
            _          => "🎭"
        };

        private static string GetEmotionColor(string emotion) => emotion.ToLower() switch
        {
            "happy"    => "#27AE60",
            "neutral"  => "#7F8C8D",
            "sad"      => "#2980B9",
            "angry"    => "#E74C3C",
            "fear"     => "#8E44AD",
            "surprise" => "#F39C12",
            "disgust"  => "#16A085",
            _          => "#34495E"
        };

        private static async Task ShowAlert(string title, string message)
        {
            if (Application.Current?.Windows.Count > 0)
                await Application.Current.Windows[0].Page!.DisplayAlert(title, message, "Tamam");
        }
    }

    public class RoomCardItem
    {
        public string RoomName { get; set; } = "";
        public string RequiredEmotion { get; set; } = "";
        public string EmotionEmoji { get; set; } = "";
        public string EmotionColor { get; set; } = "#333";
        public string DisplayText => $"Gerekli Duygu: {RequiredEmotion}";
    }
}
