using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FaceEmotionApp.Services;
using System.Collections.ObjectModel;

namespace FaceEmotionApp.ViewModels
{
    public partial class LogViewModel : ObservableObject
    {
        private readonly ApiService _apiService;

        public LogViewModel()
        {
            _apiService = new ApiService();
            Logs = new ObservableCollection<LogItem>();
        }

        public ObservableCollection<LogItem> Logs { get; }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        private bool _isEmpty;
        public bool IsEmpty { get => _isEmpty; set => SetProperty(ref _isEmpty, value); }

        public async Task LoadLogsAsync()
        {
            IsBusy = true;
            try
            {
                var logs = await _apiService.GetLogsAsync(200);
                Logs.Clear();
                foreach (var log in logs)
                    Logs.Add(ParseLogLine(log));
                IsEmpty = Logs.Count == 0;
            }
            catch { IsEmpty = true; }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task ClearLogsAsync()
        {
            bool confirm = await ConfirmAsync("Logları Temizle", "Tüm log kayıtları silinsin mi?");
            if (!confirm) return;

            IsBusy = true;
            try
            {
                await _apiService.ClearLogsAsync();
                Logs.Clear();
                IsEmpty = true;
            }
            catch { }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task RefreshAsync() => await LoadLogsAsync();

        [RelayCommand]
        public async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("//AdminPage");
        }

        private static LogItem ParseLogLine(string line)
        {
            // Format: "2024-01-01 12:00:00 | Kişi: X | Etkinlik: Y | Oda: Z | Sonuç: W | Detay: ..."
            var item = new LogItem { RawText = line };
            var parts = line.Split('|');
            if (parts.Length >= 1) item.Timestamp = parts[0].Trim();
            if (parts.Length >= 2) item.Person = parts[1].Replace("Kişi:", "").Trim();
            if (parts.Length >= 3) item.Event = parts[2].Replace("Etkinlik:", "").Trim();
            if (parts.Length >= 4) item.Room = parts[3].Replace("Oda:", "").Trim();
            if (parts.Length >= 5) item.Result = parts[4].Replace("Sonuç:", "").Trim();
            if (parts.Length >= 6) item.Detail = parts[5].Replace("Detay:", "").Trim();
            return item;
        }

        private static async Task<bool> ConfirmAsync(string title, string message)
        {
            if (Application.Current?.Windows.Count > 0)
                return await Application.Current.Windows[0].Page!.DisplayAlert(title, message, "Evet", "Hayır");
            return false;
        }
    }

    public class LogItem
    {
        public string RawText { get; set; } = "";
        public string Timestamp { get; set; } = "";
        public string Person { get; set; } = "-";
        public string Event { get; set; } = "";
        public string Room { get; set; } = "-";
        public string Result { get; set; } = "";
        public string Detail { get; set; } = "";

        public string ResultColor => Result.ToLower() switch
        {
            "onaylandı" => "#2ECC71",
            "reddedildi" => "#E74C3C",
            "bilinmeyen kişi" => "#F39C12",
            "başarılı" => "#2ECC71",
            _ => "#8899AA"
        };

        public string ResultEmoji => Result.ToLower() switch
        {
            "onaylandı" => "✅",
            "reddedildi" => "❌",
            "bilinmeyen kişi" => "❓",
            "başarılı" => "✅",
            _ => "ℹ️"
        };
    }
}
