using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Stackd.Models;
using Stackd.Services;
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

    private async void OnLinkSteamClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SteamLinkDialog(App.GetService<ISteamService>(), App.GetService<ISecretsService>())
        {
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        await ViewModel.RefreshSteamStateAsync();

        // Primera importación automática al vincular la cuenta.
        if (ViewModel.SyncSteamCommand.CanExecute(null))
            await ViewModel.SyncSteamCommand.ExecuteAsync(null);
    }

    private async void OnManageSteamClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SteamLinkDialog(App.GetService<ISteamService>(), App.GetService<ISecretsService>())
        {
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Secondary)
        {
            await ViewModel.UnlinkSteamAsync();
        }
        else if (result == ContentDialogResult.Primary)
        {
            await ViewModel.RefreshSteamStateAsync();
            if (ViewModel.SyncSteamCommand.CanExecute(null))
                await ViewModel.SyncSteamCommand.ExecuteAsync(null);
        }
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
