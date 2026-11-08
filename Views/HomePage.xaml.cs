using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Stackd.Models;
using Stackd.ViewModels;

namespace Stackd.Views;

public sealed partial class HomePage : Page
{
    public HomeViewModel ViewModel { get; }

    public HomePage()
    {
        ViewModel = App.GetService<HomeViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }

    private async void OnContinueClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not Game game) return;

        var error = await ViewModel.LaunchAsync(game);
        if (error is null) return;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Can't launch the game",
            Content = error,
            CloseButtonText = "OK"
        };
        await dialog.ShowAsync();
    }
}
