using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FaceEmotionApp.Services;

namespace FaceEmotionApp.ViewModels
{
    [QueryProperty(nameof(RoomName), "roomName")]
    [QueryProperty(nameof(RequiredEmotion), "requiredEmotion")]
    public partial class RoomAccessViewModel : ObservableObject
    {
        private readonly ApiService _apiService;

        public RoomAccessViewModel()
        {
            _apiService = new ApiService();
        }

        // ================================================
        // QUERY PARAMETRELERİ
        // ================================================

        private string _roomName = "";
        public string RoomName
        {
            get => _roomName;
            set
            {
                SetProperty(ref _roomName, Uri.UnescapeDataString(value ?? ""));
            }
        }

        private string _requiredEmotion = "";
        public string RequiredEmotion
        {
            get => _requiredEmotion;
            set
            {
                SetProperty(ref _requiredEmotion, Uri.UnescapeDataString(value ?? ""));
                OnPropertyChanged(nameof(RequiredEmotionEmoji));
                OnPropertyChanged(nameof(RequiredEmotionDisplay));
            }
        }

        public string RequiredEmotionEmoji => GetEmotionEmoji(RequiredEmotion);
        public string RequiredEmotionDisplay => $"Gerekli Duygu: {RequiredEmotion}  {RequiredEmotionEmoji}";

        // ================================================
        // SONUÇ ÖZELLİKLERİ
        // ================================================

        private string? _imagePath;
        public string? ImagePath
        {
            get => _imagePath;
            set { if (SetProperty(ref _imagePath, value)) OnPropertyChanged(nameof(HasImage)); }
        }
        public bool HasImage => !string.IsNullOrEmpty(ImagePath);

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private bool _showResult;
        public bool ShowResult
        {
            get => _showResult;
            set => SetProperty(ref _showResult, value);
        }

        private bool _accessGranted;
        public bool AccessGranted
        {
            get => _accessGranted;
            set => SetProperty(ref _accessGranted, value);
        }

        private string _resultTitle = "";
        public string ResultTitle
        {
            get => _resultTitle;
            set => SetProperty(ref _resultTitle, value);
        }

        private string _resultMessage = "";
        public string ResultMessage
        {
            get => _resultMessage;
            set => SetProperty(ref _resultMessage, value);
        }

        private string _personInfo = "";
        public string PersonInfo
        {
            get => _personInfo;
            set => SetProperty(ref _personInfo, value);
        }

        private string _emotionInfo = "";
        public string EmotionInfo
        {
            get => _emotionInfo;
            set => SetProperty(ref _emotionInfo, value);
        }

        // ================================================
        // KOMUTLAR
        // ================================================

        [RelayCommand]
        public async Task CaptureAndCheckAsync()
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await ShowAlert("Hata", "Kamera bu cihazda desteklenmiyor.");
                return;
            }

            ShowResult = false;

            try
            {
                var photo = await MediaPicker.Default.CapturePhotoAsync();
                if (photo == null) return;

                string localFilePath = Path.Combine(FileSystem.CacheDirectory, photo.FileName);
                using (Stream sourceStream = await photo.OpenReadAsync())
                using (FileStream localFileStream = File.OpenWrite(localFilePath))
                    await sourceStream.CopyToAsync(localFileStream);

                ImagePath = localFilePath;
                IsBusy = true;

                var result = await _apiService.CheckRoomAccessAsync(localFilePath, RoomName);
                ProcessResult(result);
            }
            catch (Exception ex)
            {
                AccessGranted = false;
                ResultTitle = "Bağlantı Hatası";
                ResultMessage = ex.Message;
                ShowResult = true;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task GoBackToDashboardAsync()
        {
            ShowResult = false;
            ImagePath = null;
            await Shell.Current.GoToAsync("//DashboardPage");
        }

        [RelayCommand]
        public void TryAgain()
        {
            ShowResult = false;
            ImagePath = null;
        }

        // ================================================
        // YARDIMCILAR
        // ================================================

        private void ProcessResult(AccessCheckResult result)
        {
            PersonInfo = result.Person != null ? $"Kişi: {result.Person}  ({result.Role})" : "";
            EmotionInfo = result.Dominant_Emotion != null
                ? $"Tespit edilen duygu: {result.Dominant_Emotion} {GetEmotionEmoji(result.Dominant_Emotion)}"
                : "";

            switch (result.Status)
            {
                case "access_granted":
                    AccessGranted = true;
                    ResultTitle = "✅ Giriş İzni Verildi!";
                    ResultMessage = result.Message ?? "";
                    break;

                case "access_denied_emotion":
                    AccessGranted = false;
                    ResultTitle = "❌ Duygu Uyuşmuyor";
                    ResultMessage = $"Bu odaya girmek için '{result.Required_Emotion} {GetEmotionEmoji(result.Required_Emotion ?? "")}' duygusunda olmalısınız.\nTespit edilen: {result.Dominant_Emotion} {GetEmotionEmoji(result.Dominant_Emotion ?? "")}";
                    break;

                case "access_denied_role":
                    AccessGranted = false;
                    ResultTitle = "❌ Yetki Yok";
                    ResultMessage = result.Message ?? "Bu odaya erişim yetkiniz bulunmamaktadır.";
                    break;

                case "unknown_person":
                    AccessGranted = false;
                    ResultTitle = "❌ Kişi Tanınamadı";
                    ResultMessage = "Sisteme kayıtlı olmayan bir kişi. Lütfen önce giriş yapın.";
                    PersonInfo = "";
                    EmotionInfo = "";
                    break;

                case "no_face":
                    AccessGranted = false;
                    ResultTitle = "❌ Yüz Tespit Edilemedi";
                    ResultMessage = "Fotoğrafta insan yüzü bulunamadı. Lütfen tekrar deneyin.";
                    PersonInfo = "";
                    EmotionInfo = "";
                    break;

                case "no_role":
                    AccessGranted = false;
                    ResultTitle = "❌ Rol Atanmamış";
                    ResultMessage = $"{result.Person} kişisine henüz bir rol atanmamış.";
                    break;

                default:
                    AccessGranted = false;
                    ResultTitle = "Hata";
                    ResultMessage = result.Message ?? "Bilinmeyen bir hata oluştu.";
                    break;
            }

            ShowResult = true;
        }

        private static string GetEmotionEmoji(string? emotion) => (emotion ?? "").ToLower() switch
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

        private static async Task ShowAlert(string title, string message)
        {
            if (Application.Current?.Windows.Count > 0)
                await Application.Current.Windows[0].Page!.DisplayAlert(title, message, "Tamam");
        }
    }
}
