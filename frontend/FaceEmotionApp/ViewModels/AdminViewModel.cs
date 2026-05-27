using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FaceEmotionApp.Services;
using System.Collections.ObjectModel;

namespace FaceEmotionApp.ViewModels
{
    public partial class AdminViewModel : ObservableObject
    {
        private readonly ApiService _apiService;

        public AdminViewModel()
        {
            _apiService = new ApiService();
            Rooms = new ObservableCollection<RoomAdminItem>();
            Roles = new ObservableCollection<RoleAdminItem>();
            Persons = new ObservableCollection<PersonAdminItem>();
            RoleRoomPermissions = new ObservableCollection<RoomRoleToggleItem>();
            AvailableEmotions = new List<string> { "happy", "neutral", "sad", "angry", "fear", "surprise", "disgust" };
            LoadData();
        }

        // ================================================
        // ÖZELLİKLER
        // ================================================

        public ObservableCollection<RoomAdminItem> Rooms { get; }
        public ObservableCollection<RoleAdminItem> Roles { get; }
        public ObservableCollection<PersonAdminItem> Persons { get; }
        public ObservableCollection<RoomRoleToggleItem> RoleRoomPermissions { get; }
        public List<string> AvailableEmotions { get; }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        private string _statusMessage = "";
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        private bool _hasStatus;
        public bool HasStatus { get => _hasStatus; set => SetProperty(ref _hasStatus, value); }

        private bool _isSuccess;
        public bool IsSuccess { get => _isSuccess; set => SetProperty(ref _isSuccess, value); }

        // --- Yeni Oda ---
        private string _newRoomName = "";
        public string NewRoomName { get => _newRoomName; set => SetProperty(ref _newRoomName, value); }

        private string _newRoomEmotion = "happy";
        public string NewRoomEmotion { get => _newRoomEmotion; set => SetProperty(ref _newRoomEmotion, value); }

        // --- Yeni Rol ---
        private string _newRoleName = "";
        public string NewRoleName { get => _newRoleName; set => SetProperty(ref _newRoleName, value); }

        // Seçilen rol için oda izinleri (checkbox listesi için)
        private RoleAdminItem? _selectedRole;
        public RoleAdminItem? SelectedRole
        {
            get => _selectedRole;
            set
            {
                if (SetProperty(ref _selectedRole, value))
                {
                    OnPropertyChanged(nameof(HasSelectedRole));
                    LoadRolePermissions();
                }
            }
        }
        public bool HasSelectedRole => SelectedRole != null;
        // Seçilen oda için duygu düzenleme
        private RoomAdminItem? _selectedRoom;
        public RoomAdminItem? SelectedRoom
        {
            get => _selectedRoom;
            set
            {
                if (SetProperty(ref _selectedRoom, value))
                {
                    OnPropertyChanged(nameof(HasSelectedRoom));
                }
            }
        }
        public bool HasSelectedRoom => SelectedRoom != null;

        private void LoadRolePermissions()
        {
            RoleRoomPermissions.Clear();
            if (SelectedRole == null) return;

            var allowedRooms = SelectedRole.AllowedRoomsText
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(r => r.Trim())
                .ToList();

            foreach (var room in Rooms)
            {
                RoleRoomPermissions.Add(new RoomRoleToggleItem
                {
                    RoomName = room.RoomName,
                    IsAllowed = allowedRooms.Contains(room.RoomName)
                });
            }
        }

        // ================================================
        // VERİ YÜKLEME
        // ================================================

        private async void LoadData()
        {
            IsBusy = true;
            try
            {
                // Odaları yükle
                var rooms = await _apiService.GetRoomsAsync();
                Rooms.Clear();
                foreach (var r in rooms)
                    Rooms.Add(new RoomAdminItem { RoomName = r.Name ?? "", RequiredEmotion = r.Required_Emotion ?? "happy" });

                // Rolleri yükle
                var roles = await _apiService.GetRolesAsync();
                Roles.Clear();
                foreach (var r in roles)
                    Roles.Add(new RoleAdminItem
                    {
                        RoleName = r.Name ?? "",
                        AllowedRoomsText = string.Join(", ", r.Allowed_Rooms ?? new List<string>()),
                        IsMudur = r.Name == "Müdür"
                    });

                // Kişileri yükle
                var persons = await _apiService.GetPersonsAsync();
                Persons.Clear();
                foreach (var p in persons)
                    Persons.Add(new PersonAdminItem 
                    { 
                        PersonName = p.Name ?? "", 
                        CurrentRole = p.Role ?? "",
                        AvailableRoles = roles.Select(r => r.Name ?? "").ToList()
                    });
            }
            catch (Exception ex)
            {
                ShowStatus($"Veri yüklenemedi: {ex.Message}", false);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            HasStatus = false;
            Rooms.Clear();
            Roles.Clear();
            Persons.Clear();
            LoadData();
            await Task.CompletedTask;
        }

        [RelayCommand]
        public async Task GoToLogsAsync()
        {
            await Shell.Current.GoToAsync("//LogPage");
        }

        [RelayCommand]
        public async Task GoToAiAnalysisAsync()
        {
            await Shell.Current.GoToAsync("//AiAnalysisPage");
        }

        // ================================================
        // ODA İŞLEMLERİ
        // ================================================

        [RelayCommand]
        public async Task AddRoomAsync()
        {
            if (string.IsNullOrWhiteSpace(NewRoomName))
            { ShowStatus("Oda adı boş olamaz.", false); return; }

            IsBusy = true;
            try
            {
                await _apiService.AddRoomAsync(NewRoomName.Trim(), NewRoomEmotion);
                Rooms.Add(new RoomAdminItem { RoomName = NewRoomName.Trim(), RequiredEmotion = NewRoomEmotion });
                ShowStatus($"'{NewRoomName}' odası eklendi.", true);
                NewRoomName = "";
            }
            catch (Exception ex) { ShowStatus(ex.Message, false); }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task DeleteRoomAsync(RoomAdminItem room)
        {
            bool confirm = await ConfirmAsync("Odayı Sil", $"'{room.RoomName}' odası silinsin mi?");
            if (!confirm) return;

            IsBusy = true;
            try
            {
                await _apiService.DeleteRoomAsync(room.RoomName);
                Rooms.Remove(room);
                ShowStatus($"'{room.RoomName}' odası silindi.", true);
            }
            catch (Exception ex) { ShowStatus(ex.Message, false); }
            finally { IsBusy = false; }
        }

        // ================================================
        // ROL İŞLEMLERİ
        // ================================================

        [RelayCommand]
        public async Task AddRoleAsync()
        {
            if (string.IsNullOrWhiteSpace(NewRoleName))
            { ShowStatus("Rol adı boş olamaz.", false); return; }

            IsBusy = true;
            try
            {
                await _apiService.AddRoleAsync(NewRoleName.Trim(), "");
                Roles.Add(new RoleAdminItem { RoleName = NewRoleName.Trim(), AllowedRoomsText = "" });
                ShowStatus($"'{NewRoleName}' rolü eklendi.", true);
                NewRoleName = "";
            }
            catch (Exception ex) { ShowStatus(ex.Message, false); }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task DeleteRoleAsync(RoleAdminItem role)
        {
            if (role.IsMudur)
            { ShowStatus("Müdür rolü silinemez.", false); return; }

            bool confirm = await ConfirmAsync("Rolü Sil", $"'{role.RoleName}' rolü silinsin mi?");
            if (!confirm) return;

            IsBusy = true;
            try
            {
                await _apiService.DeleteRoleAsync(role.RoleName);
                Roles.Remove(role);
                ShowStatus($"'{role.RoleName}' rolü silindi.", true);
            }
            catch (Exception ex) { ShowStatus(ex.Message, false); }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task SaveRolePermissionsAsync()
        {
            if (SelectedRole == null) return;

            IsBusy = true;
            var currentAllowed = RoleRoomPermissions
                .Where(p => p.IsAllowed)
                .Select(p => p.RoomName)
                .ToList();

            string newRoomsStr = string.Join(",", currentAllowed);
            try
            {
                await _apiService.UpdateRoleRoomsAsync(SelectedRole.RoleName, newRoomsStr);
                SelectedRole.AllowedRoomsText = newRoomsStr;
                ShowStatus($"{SelectedRole.RoleName} izinleri güncellendi.", true);
                SelectedRole = null; // Kapat
            }
            catch (Exception ex) 
            { 
                ShowStatus(ex.Message, false); 
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public void SelectRole(RoleAdminItem role)
        {
            SelectedRole = role;
        }

        [RelayCommand]
        public void ClosePermissions()
        {
            SelectedRole = null;
        }

        // ================================================
        // KİŞİ ROL DÜZENLEME
        // ================================================

        private PersonAdminItem? _selectedPerson;
        public PersonAdminItem? SelectedPerson
        {
            get => _selectedPerson;
            set
            {
                if (SetProperty(ref _selectedPerson, value))
                    OnPropertyChanged(nameof(HasSelectedPerson));
            }
        }
        public bool HasSelectedPerson => SelectedPerson != null;

        [RelayCommand]
        public void SelectPerson(PersonAdminItem person)
        {
            SelectedPerson = person;
        }

        [RelayCommand]
        public void ClosePersonEdit()
        {
            SelectedPerson = null;
        }

        [RelayCommand]
        public async Task SavePersonRoleAsync()
        {
            if (SelectedPerson == null) return;

            IsBusy = true;
            try
            {
                await _apiService.UpdatePersonRoleAsync(SelectedPerson.PersonName, SelectedPerson.CurrentRole);
                ShowStatus($"{SelectedPerson.PersonName} rolü '{SelectedPerson.CurrentRole}' olarak güncellendi.", true);
                SelectedPerson = null;
            }
            catch (Exception ex) { ShowStatus(ex.Message, false); }
            finally { IsBusy = false; }
        }

        // ================================================
        // ODA DUYGU DÜZENLEME
        // ================================================

        [RelayCommand]
        public void SelectRoom(RoomAdminItem room)
        {
            SelectedRoom = room;
        }

        [RelayCommand]
        public void CloseRoomEdit()
        {
            SelectedRoom = null;
        }

        [RelayCommand]
        public async Task SaveRoomEmotionAsync()
        {
            if (SelectedRoom == null) return;

            IsBusy = true;
            try
            {
                await _apiService.UpdateRoomEmotionAsync(SelectedRoom.RoomName, SelectedRoom.RequiredEmotion);
                ShowStatus($"'{SelectedRoom.RoomName}' duygusu '{SelectedRoom.RequiredEmotion}' olarak güncellendi.", true);
                SelectedRoom = null; // Kapat
            }
            catch (Exception ex) { ShowStatus(ex.Message, false); }
            finally { IsBusy = false; }
        }

        // ================================================
        // NAVİGASYON
        // ================================================

        [RelayCommand]
        public async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("//DashboardPage");
        }

        // ================================================
        // YARDIMCILAR
        // ================================================

        private void ShowStatus(string message, bool success)
        {
            StatusMessage = message;
            IsSuccess = success;
            HasStatus = true;
        }

        private static async Task<bool> ConfirmAsync(string title, string message)
        {
            if (Application.Current?.Windows.Count > 0)
                return await Application.Current.Windows[0].Page!.DisplayAlert(title, message, "Evet", "Hayır");
            return false;
        }
    }

    public class RoomAdminItem : ObservableObject
    {
        public string RoomName { get; set; } = "";
        private string _requiredEmotion = "";
        public string RequiredEmotion
        {
            get => _requiredEmotion;
            set => SetProperty(ref _requiredEmotion, value);
        }
        public string EmotionEmoji => _requiredEmotion.ToLower() switch
        {
            "happy" => "😊", "neutral" => "😐", "sad" => "😢",
            "angry" => "😡", "fear" => "😨", "surprise" => "😲",
            "disgust" => "🤢", _ => "🎭"
        };
    }

    public class RoleAdminItem : ObservableObject
    {
        public string RoleName { get; set; } = "";
        private string _allowedRoomsText = "";
        public string AllowedRoomsText
        {
            get => _allowedRoomsText;
            set => SetProperty(ref _allowedRoomsText, value);
        }
        public bool IsMudur { get; set; }
        public string AllowedRoomsDisplay => string.IsNullOrEmpty(AllowedRoomsText)
            ? "Oda izni yok" : AllowedRoomsText;
    }

    public class RoomRoleToggleItem : ObservableObject
    {
        public string RoomName { get; set; } = "";
        private bool _isAllowed;
        public bool IsAllowed
        {
            get => _isAllowed;
            set => SetProperty(ref _isAllowed, value);
        }
    }

    public class PersonAdminItem : ObservableObject
    {
        public string PersonName { get; set; } = "";
        private string _currentRole = "";
        public string CurrentRole
        {
            get => _currentRole;
            set => SetProperty(ref _currentRole, value);
        }
        public List<string> AvailableRoles { get; set; } = new();
    }
}
