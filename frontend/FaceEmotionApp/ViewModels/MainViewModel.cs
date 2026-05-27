using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FaceEmotionApp.Services;
using FaceEmotionApp.Views;

namespace FaceEmotionApp.ViewModels
{
    public enum LoginFlowState
    {
        FaceCapture,         // İlk ekran: yüz tara
        RegisterNewPerson,   // Bilinmeyen yüz: isim + rol + fotoğraf
        AssignRole,          // Yüz biliniyor ama rolü yok: rol seç
    }

    public partial class MainViewModel : ObservableObject
    {
        private readonly ApiService _apiService;

        public MainViewModel()
        {
            _apiService = new ApiService();
            CurrentState = LoginFlowState.FaceCapture;
            LoadRoles();
        }

        // ================================================
        // DURUM MAKİNESİ
        // ================================================

        private LoginFlowState _currentState;
        public LoginFlowState CurrentState
        {
            get => _currentState;
            set
            {
                if (SetProperty(ref _currentState, value))
                {
                    OnPropertyChanged(nameof(IsFaceCaptureState));
                    OnPropertyChanged(nameof(IsRegisterNewPersonState));
                    OnPropertyChanged(nameof(IsAssignRoleState));
                }
            }
        }

        public bool IsFaceCaptureState    => CurrentState == LoginFlowState.FaceCapture;
        public bool IsRegisterNewPersonState => CurrentState == LoginFlowState.RegisterNewPerson;
        public bool IsAssignRoleState     => CurrentState == LoginFlowState.AssignRole;

        // ================================================
        // ÖZELLİKLER
        // ================================================

        private string? _imagePath;
        public string? ImagePath
        {
            get => _imagePath;
            set { if (SetProperty(ref _imagePath, value)) OnPropertyChanged(nameof(HasImage)); }
        }
        public bool HasImage => !string.IsNullOrEmpty(ImagePath);

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        // Kayıt formu
        private string _personNameToRegister = "";
        public string PersonNameToRegister
        {
            get => _personNameToRegister;
            set => SetProperty(ref _personNameToRegister, value);
        }

        // Rol atama (tanınan kişi için)
        private string _recognizedPersonName = "";
        public string RecognizedPersonName
        {
            get => _recognizedPersonName;
            set => SetProperty(ref _recognizedPersonName, value);
        }

        // Roller listesi (dropdown için)
        private List<string> _availableRoles = new();
        public List<string> AvailableRoles
        {
            get => _availableRoles;
            set => SetProperty(ref _availableRoles, value);
        }

        private string? _selectedRole;
        public string? SelectedRole
        {
            get => _selectedRole;
            set => SetProperty(ref _selectedRole, value);
        }

        // ================================================
        // YARDIMCI: ROLLERİ YÜKLE
        // ================================================

        private async void LoadRoles()
        {
            try
            {
                var roles = await _apiService.GetRolesAsync();
                AvailableRoles = roles.Select(r => r.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList();
            }
            catch { /* Sunucu henüz hazır değilse sessizce geç */ }
        }

        // ================================================
        // KOMUTLAR
        // ================================================

        [RelayCommand]
        public async Task CaptureAndLoginAsync()
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await ShowAlert("Hata", "Kamera bu cihazda desteklenmiyor.");
                return;
            }

            try
            {
                var photo = await MediaPicker.Default.CapturePhotoAsync();
                if (photo == null) return;

                string localFilePath = await SavePhotoAsync(photo);
                ImagePath = localFilePath;

                IsBusy = true;
                StatusMessage = "Yüz tanınıyor...";

                var result = await _apiService.RecognizeFaceAsync(localFilePath);

                switch (result?.Status)
                {
                    case "found":
                        await HandleRecognizedPerson(result);
                        break;

                    case "no_face":
                        ImagePath = null;
                        await ShowAlert("İnsan Değil", "Fotoğrafta insan yüzü tespit edilemedi. Lütfen tekrar deneyin.");
                        StatusMessage = "";
                        break;

                    case "unknown":
                    default:
                        // Bilinmeyen kişi → Kayıt ekranına yönlendir
                        StatusMessage = "Kişi bulunamadı. Lütfen kayıt olun.";
                        if (AvailableRoles.Count == 0) await LoadRolesAsync();
                        CurrentState = LoginFlowState.RegisterNewPerson;
                        break;
                }
            }
            catch (Exception ex)
            {
                await ShowAlert("Hata", $"Hata oluştu: {ex.Message}");
                StatusMessage = "";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task HandleRecognizedPerson(AnalysisResult result)
        {
            string personName = result.Person ?? "Bilinmiyor";
            string role = result.Role ?? "";

            if (string.IsNullOrEmpty(role))
            {
                // Yüz tanındı ama rol yok → Rol atama ekranına
                RecognizedPersonName = personName;
                StatusMessage = $"Merhaba {personName}! Rol seçimi gerekiyor.";
                if (AvailableRoles.Count == 0) await LoadRolesAsync();
                CurrentState = LoginFlowState.AssignRole;
            }
            else
            {
                // Tam giriş
                await CompleteLoginAsync(personName, role);
            }
        }

        [RelayCommand]
        public async Task AssignRoleAndLoginAsync()
        {
            if (string.IsNullOrEmpty(SelectedRole))
            {
                await ShowAlert("Uyarı", "Lütfen bir rol seçin.");
                return;
            }

            IsBusy = true;
            try
            {
                await _apiService.AssignPersonRoleAsync(RecognizedPersonName, SelectedRole);
                await CompleteLoginAsync(RecognizedPersonName, SelectedRole);
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
        public async Task RegisterAndLoginAsync()
        {
            if (string.IsNullOrWhiteSpace(PersonNameToRegister))
            {
                await ShowAlert("Uyarı", "Lütfen adınızı girin.");
                return;
            }
            if (string.IsNullOrEmpty(SelectedRole))
            {
                await ShowAlert("Uyarı", "Lütfen bir rol seçin.");
                return;
            }
            if (string.IsNullOrEmpty(ImagePath))
            {
                await ShowAlert("Uyarı", "Lütfen önce fotoğraf çekin.");
                return;
            }

            IsBusy = true;
            try
            {
                string name = PersonNameToRegister.Trim();
                var result = await _apiService.RegisterPersonAsync(ImagePath, name, SelectedRole);
                if (result?.Status == "success")
                {
                    await CompleteLoginAsync(name, SelectedRole);
                }
                else
                {
                    await ShowAlert("Hata", "Kayıt işlemi başarısız oldu.");
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
        public void ResetToLogin()
        {
            ImagePath = null;
            StatusMessage = "";
            PersonNameToRegister = "";
            RecognizedPersonName = "";
            SelectedRole = null;
            UserSession.Current.Clear();
            CurrentState = LoginFlowState.FaceCapture;
        }

        // ================================================
        // YARDIMCI METODLAR
        // ================================================

        private async Task CompleteLoginAsync(string personName, string role)
        {
            // Kullanıcının rolüne göre izinli odaları bul
            var roles = await _apiService.GetRolesAsync();
            var matchedRole = roles.FirstOrDefault(r => r.Name == role);
            var allowedRooms = matchedRole?.Allowed_Rooms ?? new List<string>();

            // Oturumu başlat
            UserSession.Current.SetUser(personName, role, allowedRooms);

            // Dashboard'a git
            await Shell.Current.GoToAsync("//DashboardPage");
        }

        private async Task LoadRolesAsync()
        {
            var roles = await _apiService.GetRolesAsync();
            AvailableRoles = roles.Select(r => r.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList();
        }

        private static async Task<string> SavePhotoAsync(FileResult photo)
        {
            string localFilePath = Path.Combine(FileSystem.CacheDirectory, photo.FileName);
            using Stream sourceStream = await photo.OpenReadAsync();
            using FileStream localFileStream = File.OpenWrite(localFilePath);
            await sourceStream.CopyToAsync(localFileStream);
            return localFilePath;
        }

        private static async Task ShowAlert(string title, string message)
        {
            if (Application.Current?.Windows.Count > 0)
                await Application.Current.Windows[0].Page!.DisplayAlert(title, message, "Tamam");
        }
    }
}
