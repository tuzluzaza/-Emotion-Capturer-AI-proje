using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FaceEmotionApp.Services;

namespace FaceEmotionApp.ViewModels
{
    // CommunityToolkit.Mvvm kütüphanesini NuGet üzerinden projenize eklemelisiniz.
    // ObservableObject sınıfı verilerin UI ile senkronize olmasını sağlar (Data Binding).
    public partial class MainViewModel : ObservableObject
    {
        private readonly ApiService _apiService;

        public MainViewModel()
        {
            _apiService = new ApiService();
        }

        [ObservableProperty]
        private string imagePath;

        [ObservableProperty]
        private string personName = "Kişi Bekleniyor...";

        [ObservableProperty]
        private string dominantEmotion = "Duygu Bekleniyor...";

        [ObservableProperty]
        private bool isBusy;

        [RelayCommand]
        public async Task TakePhotoAsync()
        {
            try
            {
                if (MediaPicker.Default.IsCaptureSupported)
                {
                    // Kamerayı aç ve fotoğraf çek
                    FileResult photo = await MediaPicker.Default.CapturePhotoAsync();

                    if (photo != null)
                    {
                        // Fotoğrafı geçici bir dizine kaydet (Cache)
                        string localFilePath = Path.Combine(FileSystem.CacheDirectory, photo.FileName);

                        using Stream sourceStream = await photo.OpenReadAsync();
                        using FileStream localFileStream = File.OpenWrite(localFilePath);

                        await sourceStream.CopyToAsync(localFileStream);
                        localFileStream.Close(); // Dosyayı serbest bırak

                        // Ekranda göstermek için resim yolunu güncelle
                        ImagePath = localFilePath;

                        // Fotoğraf çekildikten sonra otomatik olarak analize gönder
                        await AnalyzePhotoAsync(localFilePath);
                    }
                }
                else
                {
                    await App.Current.MainPage.DisplayAlert("Hata", "Kamera desteklenmiyor.", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await App.Current.MainPage.DisplayAlert("Hata", $"Fotoğraf çekilirken hata oluştu: {ex.Message}", "Tamam");
            }
        }

        private async Task AnalyzePhotoAsync(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            IsBusy = true;
            PersonName = "Analiz ediliyor...";
            DominantEmotion = "Analiz ediliyor...";

            try
            {
                // API servisini çağır
                var result = await _apiService.AnalyzeFaceAsync(path);

                if (result != null)
                {
                    // Sonuçları UI'a yansıt
                    PersonName = result.Person;
                    DominantEmotion = result.Dominant_Emotion;
                }
            }
            catch (Exception ex)
            {
                await App.Current.MainPage.DisplayAlert("API Hatası", ex.Message, "Tamam");
                PersonName = "Hata oluştu";
                DominantEmotion = "Hata oluştu";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
