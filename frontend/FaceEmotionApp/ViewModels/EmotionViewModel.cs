using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FaceEmotionApp.Services;

namespace FaceEmotionApp.ViewModels
{
    public partial class EmotionViewModel : ObservableObject
    {
        private readonly ApiService _apiService;

        public EmotionViewModel()
        {
            _apiService = new ApiService();
        }

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

        private string _personName = "";
        public string PersonName
        {
            get => _personName;
            set => SetProperty(ref _personName, value);
        }

        private string _dominantEmotion = "";
        public string DominantEmotion
        {
            get => _dominantEmotion;
            set => SetProperty(ref _dominantEmotion, value);
        }

        private bool _showResult;
        public bool ShowResult
        {
            get => _showResult;
            set => SetProperty(ref _showResult, value);
        }

        [RelayCommand]
        public async Task AnalyzeAsync()
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
                using (Stream s = await photo.OpenReadAsync())
                using (FileStream fs = File.OpenWrite(localFilePath))
                    await s.CopyToAsync(fs);

                ImagePath = localFilePath;
                IsBusy = true;

                // Önce yüz tanı
                var recog = await _apiService.RecognizeFaceAsync(localFilePath);
                if (recog?.Status == "no_face")
                {
                    await ShowAlert("İnsan Değil", "Fotoğrafta insan yüzü tespit edilemedi.");
                    ImagePath = null;
                    return;
                }

                PersonName = recog?.Status == "found"
                    ? (recog.Person ?? "Bilinmiyor")
                    : "Kayıtlı olmayan kişi";

                // Sonra duygu analizi
                var emotion = await _apiService.AnalyzeEmotionAsync(localFilePath);
                DominantEmotion = emotion?.Status == "success"
                    ? $"{emotion.Dominant_Emotion} {GetEmoji(emotion.Dominant_Emotion)}"
                    : "Analiz Başarısız";

                ShowResult = true;

                // Kayıtlı değilse kaydet mi sor
                if (recog?.Status != "found")
                {
                    IsBusy = false;
                    bool answer = await Application.Current!.Windows[0].Page!.DisplayAlert(
                        "Kayıtlı Değil",
                        "Bu kişi sistemde kayıtlı değil. Kaydetmek ister misiniz?",
                        "Evet", "Hayır");
                    if (answer)
                        await Shell.Current.GoToAsync("//MainPage");
                }
            }
            catch (Exception ex)
            {
                await ShowAlert("Hata", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void ResetAnalysis()
        {
            ImagePath = null;
            PersonName = "";
            DominantEmotion = "";
            ShowResult = false;
        }

        [RelayCommand]
        public async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("//DashboardPage");
        }

        private static string GetEmoji(string? emotion) => (emotion ?? "").ToLower() switch
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
