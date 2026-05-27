using System.Net.Http.Headers;
using System.Text.Json;

namespace FaceEmotionApp.Services
{
    public class ApiService
    {
        // Android emulator kullanıyorsanız 10.0.2.2 veya kendi bilgisayarınızın IP adresini yazın.
        // Windows Machine (Local) için localhost veya 127.0.0.1 kullanılabilir.
        private readonly string _baseUrl = "http://10.0.2.2:8000"; 
        private readonly HttpClient _httpClient;

        public ApiService()
        {
            _httpClient = new HttpClient();
            // Zaman aşımını biraz uzun tutalım (DeepFace analizi zaman alabilir)
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
        }

        public async Task<AnalysisResult> AnalyzeFaceAsync(string imagePath)
        {
            try
            {
                // Fotoğraf dosyasını okuyup MultipartFormDataContent'e ekliyoruz
                using var multipartFormContent = new MultipartFormDataContent();
                
                var fileStreamContent = new StreamContent(File.OpenRead(imagePath));
                fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                
                // FastAPI'deki "file" parametresi ile eşleşmeli
                multipartFormContent.Add(fileStreamContent, name: "file", fileName: Path.GetFileName(imagePath));

                // API'ye POST isteği gönderiyoruz
                var response = await _httpClient.PostAsync($"{_baseUrl}/analyze", multipartFormContent);
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    
                    // Gelen JSON sonucunu modele dönüştürüyoruz
                    var result = JsonSerializer.Deserialize<AnalysisResult>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return result;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"API Hatası: {response.StatusCode} - {error}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Bağlantı veya istek hatası: {ex.Message}");
            }
        }
    }

    // API'den dönecek JSON formatına uygun modeller
    public class AnalysisResult
    {
        public string Status { get; set; }
        public string Person { get; set; }
        public string Dominant_Emotion { get; set; }
        public Dictionary<string, double> Emotions { get; set; }
    }
}
