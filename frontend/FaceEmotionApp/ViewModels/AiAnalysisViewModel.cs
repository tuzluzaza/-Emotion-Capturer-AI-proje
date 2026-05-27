using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FaceEmotionApp.Services;

namespace FaceEmotionApp.ViewModels
{
    public partial class AiAnalysisViewModel : ObservableObject
    {
        private readonly ApiService _apiService;

        public AiAnalysisViewModel()
        {
            _apiService = new ApiService();
        }

        // ================================================
        // ÖZELLİKLER
        // ================================================

        private string _analysisText = "";
        public string AnalysisText
        {
            get => _analysisText;
            set => SetProperty(ref _analysisText, value);
        }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        private bool _hasResult;
        public bool HasResult { get => _hasResult; set => SetProperty(ref _hasResult, value); }

        private bool _hasError;
        public bool HasError { get => _hasError; set => SetProperty(ref _hasError, value); }

        private string _errorMessage = "";
        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }

        private int _logCount;
        public int LogCount { get => _logCount; set => SetProperty(ref _logCount, value); }

        public string LogCountText => $"{LogCount} log kaydı analiz edildi";

        // ================================================
        // KOMUTLAR
        // ================================================

        [RelayCommand]
        public async Task AnalyzeAsync()
        {
            IsBusy = true;
            HasResult = false;
            HasError = false;
            AnalysisText = "";

            try
            {
                var result = await _apiService.AnalyzeLogsAsync();
                AnalysisText = result.Analysis ?? "Analiz sonucu alınamadı.";
                LogCount = result.Log_Count;
                OnPropertyChanged(nameof(LogCountText));
                HasResult = true;
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("//AdminPage");
        }
    }
}
