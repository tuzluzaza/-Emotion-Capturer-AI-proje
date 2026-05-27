namespace FaceEmotionApp.Views;

public partial class DashboardPage : ContentPage
{
    public DashboardPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModels.DashboardViewModel vm)
        {
            await vm.RefreshDataAsync();
        }
    }
}
