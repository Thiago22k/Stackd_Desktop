using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Stackd.Models;
using Stackd.Services;
using Stackd.ViewModels;

namespace Stackd.Views;

public sealed partial class ExplorePage : Page
{
    public ExploreViewModel ViewModel { get; }

    public ExplorePage()
    {
        ViewModel = App.GetService<ExploreViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }

    private async void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await ViewModel.SearchAsync();
        }
    }

    private async void OnConfigureCredentialsClick(object sender, RoutedEventArgs e)
    {
        var secrets = App.GetService<ISecretsService>();
        var dialog = new IgdbCredentialsDialog(secrets) { XamlRoot = XamlRoot };
        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.SaveCredentialsAsync(dialog.ClientId, dialog.ClientSecret);
        }
    }

    private async void OnGameItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not IgdbGame game) return;

        var dialog = new IgdbGameDialog(game) { XamlRoot = XamlRoot };
        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && !game.IsInLibrary)
        {
            var added = await ViewModel.AddToLibraryAsync(game, dialog.SelectedStatus);
            if (added)
            {
                var feedback = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Title = "Added to Library",
                    Content = $"“{game.Name}” has been added to your library.",
                    CloseButtonText = "OK"
                };
                await feedback.ShowAsync();
            }
        }
    }
}

