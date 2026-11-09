using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Stackd.Models;
using Stackd.Services;
using Stackd.ViewModels;

namespace Stackd.Views;

public sealed partial class LibraryPage : Page
{
    public LibraryViewModel ViewModel { get; }

    public LibraryPage()
    {
        ViewModel = App.GetService<LibraryViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }

    private async void OnAddClick(object sender, RoutedEventArgs e) => await EditAsync(null);

    private async void OnGameClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Game game) await EditAsync(game);
    }

    private async void OnPlayContextClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is Game game)
        {
            await LaunchGameAsync(game);
        }
    }

    private async void OnAddToCollectionClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not Game game) return;

        var collections = App.GetService<ICollectionService>();
        var dialog = new CollectionPickerDialog(game, collections) { XamlRoot = XamlRoot };
        await dialog.InitializeAsync();

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await collections.SetMembershipAsync(game.Id, dialog.SelectedCollectionIds);
    }

    private async void OnEditContextClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is Game game)
        {
            await EditAsync(game);
        }
    }

    private async void OnDeleteContextClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is Game game)
        {
            await DeleteAsync(game);
        }
    }

    private async Task LaunchGameAsync(Game game)
    {
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

    private async Task DeleteAsync(Game game)
    {
        var confirm = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Delete game?",
            Content = $"“{game.Title}” will be removed from your library.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };

        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.DeleteAsync(game.Id);
        }
    }

    private async Task EditAsync(Game? game)
    {
        var dialog = new GameEditDialog(game) { XamlRoot = XamlRoot };
        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.SaveAsync(dialog.Result);
        }
        else if (result == ContentDialogResult.Secondary && game is not null)
        {
            await DeleteAsync(game);
        }
    }
}
