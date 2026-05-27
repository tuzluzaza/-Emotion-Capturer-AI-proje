namespace FaceEmotionApp.Views;

public partial class LogPage : ContentPage
{
    public LogPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModels.LogViewModel vm)
            await vm.LoadLogsAsync();
    }
}
