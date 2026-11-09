using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Stackd.Models;
using Stackd.ViewModels;

namespace Stackd.Views;

public sealed partial class CollectionDetailPage : Page
{
    public CollectionDetailViewModel ViewModel { get; }

    public CollectionDetailPage()
    {
        ViewModel = App.GetService<CollectionDetailViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is int id && await ViewModel.LoadAsync(id))
            return;

        GoBack(); // la colección ya no existe
    }

    private void OnBackClick(object sender, RoutedEventArgs e) => GoBack();

    private void GoBack()
    {
        if (Frame.CanGoBack) Frame.GoBack();
        else Frame.Navigate(typeof(CollectionsPage));
    }

    private async void OnAddGamesClick(object sender, RoutedEventArgs e)
    {
        var candidates = await ViewModel.GetCandidatesAsync();
        var dialog = new GamePickerDialog(candidates) { XamlRoot = XamlRoot };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.AddGamesAsync(dialog.SelectedGameIds);
    }

    private async void OnEditClick(object sender, RoutedEventArgs e)
    {
        var existing = new Collection
        {
            Id = ViewModel.CollectionId,
            Name = ViewModel.Name,
            Description = ViewModel.Description
        };

        var dialog = new CollectionEditDialog(existing) { XamlRoot = XamlRoot };
        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.UpdateAsync(dialog.CollectionName, dialog.CollectionDescription);
        }
        else if (result == ContentDialogResult.Secondary)
        {
            var confirm = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Delete collection?",
                Content = $"“{ViewModel.Name}” will be deleted. The games stay in your library.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };

            if (await confirm.ShowAsync() == ContentDialogResult.Primary)
            {
                await ViewModel.DeleteAsync();
                GoBack();
            }
        }
    }

    private async void OnPlayContextClick(object sender, RoutedEventArgs e)
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

    private async void OnRemoveContextClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is Game game)
            await ViewModel.RemoveGameAsync(game.Id);
    }
}
