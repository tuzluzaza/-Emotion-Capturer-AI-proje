using System.Net.Http.Headers;
using System.Text.Json;

namespace FaceEmotionApp.Services
{
    public class ApiService
    {
        private readonly string _baseUrl = "http://10.228.242.141:8000";
        private readonly HttpClient _httpClient;

        public ApiService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
        }

        // ================================================
        // YÜKLEME YARDIMCISI
        // ================================================
        private MultipartFormDataContent BuildFileContent(string imagePath)
        {
            var content = new MultipartFormDataContent();
            var fileStream = new StreamContent(File.OpenRead(imagePath));
            fileStream.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(fileStream, name: "file", fileName: Path.GetFileName(imagePath));
            return content;
        }

        // ================================================
        // MEVCUT ENDPOİNT'LER
        // ================================================

        public async Task<AnalysisResult> RecognizeFaceAsync(string imagePath)
        {
            try
            {
                using var content = BuildFileContent(imagePath);
                var response = await _httpClient.PostAsync($"{_baseUrl}/recognize", content);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<AnalysisResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                           ?? new AnalysisResult { Status = "unknown" };
                }
                var err = await response.Content.ReadAsStringAsync();
                throw new Exception($"API Hatası: {response.StatusCode} - {err}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Bağlantı veya istek hatası: {ex.Message}");
            }
        }

        public async Task<AnalysisResult> RegisterPersonAsync(string imagePath, string personName, string role = "")
        {
            try
            {
                using var content = BuildFileContent(imagePath);
                content.Add(new StringContent(personName), name: "person_name");
                if (!string.IsNullOrEmpty(role))
                    content.Add(new StringContent(role), name: "role");

                var response = await _httpClient.PostAsync($"{_baseUrl}/register", content);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<AnalysisResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                           ?? new AnalysisResult { Status = "error" };
                }
                var err = await response.Content.ReadAsStringAsync();
                throw new Exception($"API Hatası: {response.StatusCode} - {err}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Bağlantı veya istek hatası: {ex.Message}");
            }
        }

        public async Task<AnalysisResult> AnalyzeEmotionAsync(string imagePath)
        {
            try
            {
                using var content = BuildFileContent(imagePath);
                var response = await _httpClient.PostAsync($"{_baseUrl}/analyze_emotion", content);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<AnalysisResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                           ?? new AnalysisResult { Status = "error" };
                }
                var err = await response.Content.ReadAsStringAsync();
                throw new Exception($"API Hatası: {response.StatusCode} - {err}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Bağlantı veya istek hatası: {ex.Message}");
            }
        }

        // ================================================
        // YENİ ENDPOİNT'LER - ROL/ODA/KİŞİ
        // ================================================

        public async Task<List<RoleDto>> GetRolesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/roles");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<RolesResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return result?.Roles ?? new List<RoleDto>();
                }
                return new List<RoleDto>();
            }
            catch
            {
                return new List<RoleDto>();
            }
        }

        public async Task<List<RoomDto>> GetRoomsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/rooms");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<RoomsResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return result?.Rooms ?? new List<RoomDto>();
                }
                return new List<RoomDto>();
            }
            catch
            {
                return new List<RoomDto>();
            }
        }

        public async Task<List<PersonDto>> GetPersonsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/persons");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<PersonsResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return result?.Persons ?? new List<PersonDto>();
                }
                return new List<PersonDto>();
            }
            catch { return new List<PersonDto>(); }
        }

        public async Task<AnalysisResult> AssignPersonRoleAsync(string personName, string role)
        {
            try
            {
                var content = new MultipartFormDataContent();
                content.Add(new StringContent(personName), "person_name");
                content.Add(new StringContent(role), "role");
                var response = await _httpClient.PostAsync($"{_baseUrl}/persons/assign", content);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<AnalysisResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                           ?? new AnalysisResult { Status = "error" };
                }
                var err = await response.Content.ReadAsStringAsync();
                throw new Exception($"API Hatası: {response.StatusCode} - {err}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Bağlantı veya istek hatası: {ex.Message}");
            }
        }

        // ================================================
        // YÖNETİM - ODA / ROL EKLEME & SİLME
        // ================================================

        public async Task<AnalysisResult> AddRoomAsync(string roomName, string requiredEmotion)
        {
            try
            {
                var content = new MultipartFormDataContent();
                content.Add(new StringContent(roomName), "room_name");
                content.Add(new StringContent(requiredEmotion), "required_emotion");
                var response = await _httpClient.PostAsync($"{_baseUrl}/rooms/add", content);
                var json = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                    return JsonSerializer.Deserialize<AnalysisResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                           ?? new AnalysisResult { Status = "success" };
                throw new Exception(ExtractDetail(json));
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        public async Task<AnalysisResult> UpdateRoomEmotionAsync(string roomName, string requiredEmotion)
        {
            try
            {
                var content = new MultipartFormDataContent();
                content.Add(new StringContent(roomName), "room_name");
                content.Add(new StringContent(requiredEmotion), "required_emotion");
                var response = await _httpClient.PostAsync($"{_baseUrl}/rooms/update_emotion", content);
                var json = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                    return JsonSerializer.Deserialize<AnalysisResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                           ?? new AnalysisResult { Status = "success" };
                throw new Exception(ExtractDetail(json));
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        public async Task<AnalysisResult> DeleteRoomAsync(string roomName)
        {
            try
            {
                var content = new MultipartFormDataContent();
                content.Add(new StringContent(roomName), "room_name");
                var response = await _httpClient.PostAsync($"{_baseUrl}/rooms/delete", content);
                var json = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                    return JsonSerializer.Deserialize<AnalysisResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                           ?? new AnalysisResult { Status = "success" };
                throw new Exception(ExtractDetail(json));
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        public async Task<AnalysisResult> AddRoleAsync(string roleName, string allowedRooms)
        {
            try
            {
                var content = new MultipartFormDataContent();
                content.Add(new StringContent(roleName), "role_name");
                content.Add(new StringContent(allowedRooms), "allowed_rooms");
                var response = await _httpClient.PostAsync($"{_baseUrl}/roles/add", content);
                var json = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                    return JsonSerializer.Deserialize<AnalysisResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                           ?? new AnalysisResult { Status = "success" };
                throw new Exception(ExtractDetail(json));
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        public async Task<AnalysisResult> DeleteRoleAsync(string roleName)
        {
            try
            {
                var content = new MultipartFormDataContent();
                content.Add(new StringContent(roleName), "role_name");
                var response = await _httpClient.PostAsync($"{_baseUrl}/roles/delete", content);
                var json = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                    return JsonSerializer.Deserialize<AnalysisResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                           ?? new AnalysisResult { Status = "success" };
                throw new Exception(ExtractDetail(json));
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        public async Task<AnalysisResult> UpdateRoleRoomsAsync(string roleName, string allowedRooms)
        {
            try
            {
                var content = new MultipartFormDataContent();
                content.Add(new StringContent(roleName), "role_name");
                content.Add(new StringContent(allowedRooms), "allowed_rooms");
                var response = await _httpClient.PostAsync($"{_baseUrl}/roles/update_rooms", content);
                var json = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                    return JsonSerializer.Deserialize<AnalysisResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                           ?? new AnalysisResult { Status = "success" };
                throw new Exception(ExtractDetail(json));
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        private static string ExtractDetail(string json)
        {
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("detail", out var detail))
                    return detail.GetString() ?? json;
            }
            catch { }
            return json;
        }


        public async Task<AccessCheckResult> CheckRoomAccessAsync(string imagePath, string roomName)
        {
            try
            {
                using var content = BuildFileContent(imagePath);
                content.Add(new StringContent(roomName), name: "room_name");

                var response = await _httpClient.PostAsync($"{_baseUrl}/access/check", content);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<AccessCheckResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                           ?? new AccessCheckResult { Status = "error" };
                }
                var err = await response.Content.ReadAsStringAsync();
                throw new Exception($"API Hatası: {response.StatusCode} - {err}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Bağlantı veya istek hatası: {ex.Message}");
            }
        }

        // ================================================
        // KİŞİ ROL GÜNCELLEME
        // ================================================

        public async Task<AnalysisResult> UpdatePersonRoleAsync(string personName, string newRole)
        {
            try
            {
                var content = new MultipartFormDataContent();
                content.Add(new StringContent(personName), "person_name");
                content.Add(new StringContent(newRole), "new_role");
                var response = await _httpClient.PostAsync($"{_baseUrl}/persons/update_role", content);
                var json = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                    return JsonSerializer.Deserialize<AnalysisResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                           ?? new AnalysisResult { Status = "success" };
                throw new Exception(ExtractDetail(json));
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        // ================================================
        // LOG YÖNETİMİ
        // ================================================

        public async Task<List<string>> GetLogsAsync(int limit = 100)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/logs?limit={limit}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<LogsResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return result?.Logs ?? new List<string>();
                }
                return new List<string>();
            }
            catch { return new List<string>(); }
        }

        public async Task<AnalysisResult> ClearLogsAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/logs/clear", new MultipartFormDataContent());
                var json = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                    return JsonSerializer.Deserialize<AnalysisResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                           ?? new AnalysisResult { Status = "success" };
                throw new Exception(ExtractDetail(json));
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        public async Task<LogAnalysisResponse> AnalyzeLogsAsync()
        {
            try
            {
                var content = new StringContent("", System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_baseUrl}/logs/analyze", content);
                var json = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                    return JsonSerializer.Deserialize<LogAnalysisResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                           ?? new LogAnalysisResponse { Analysis = "Analiz alınamadı." };
                throw new Exception(ExtractDetail(json));
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }
    }

    // ================================================
    // VERİ MODELLERİ
    // ================================================

    public class AnalysisResult
    {
        public string? Status { get; set; }
        public string? Person { get; set; }
        public string? Role { get; set; }
        public string? Message { get; set; }
        public string? Dominant_Emotion { get; set; }
        public Dictionary<string, double>? Emotions { get; set; }
    }

    public class AccessCheckResult
    {
        public string? Status { get; set; }
        public string? Person { get; set; }
        public string? Role { get; set; }
        public string? Room { get; set; }
        public string? Dominant_Emotion { get; set; }
        public string? Required_Emotion { get; set; }
        public string? Message { get; set; }
        public Dictionary<string, double>? Emotions { get; set; }
    }

    public class RoleDto
    {
        public string? Name { get; set; }
        public List<string>? Allowed_Rooms { get; set; }
    }

    public class RoomDto
    {
        public string? Name { get; set; }
        public string? Required_Emotion { get; set; }
    }

    public class RolesResponse
    {
        public List<RoleDto>? Roles { get; set; }
    }

    public class RoomsResponse
    {
        public List<RoomDto>? Rooms { get; set; }
    }

    public class PersonDto
    {
        public string? Name { get; set; }
        public string? Role { get; set; }
    }

    public class PersonsResponse
    {
        public List<PersonDto>? Persons { get; set; }
    }

    public class LogsResponse
    {
        public List<string>? Logs { get; set; }
    }

    public class LogAnalysisResponse
    {
        public string? Analysis { get; set; }
        public int Log_Count { get; set; }
    }
}
