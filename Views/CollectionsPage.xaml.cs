using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Stackd.Models;
using Stackd.Services;
using Stackd.ViewModels;

namespace Stackd.Views;

public sealed partial class CollectionsPage : Page
{
    public CollectionsViewModel ViewModel { get; }

    public CollectionsPage()
    {
        ViewModel = App.GetService<CollectionsViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }

    private async void OnNewClick(object sender, RoutedEventArgs e)
    {
        var dialog = new CollectionEditDialog { XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.CreateAsync(dialog.CollectionName, dialog.CollectionDescription);
    }

    private void OnCollectionClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CollectionItem item)
            Frame.Navigate(typeof(CollectionDetailPage), item.Id);
    }

    private async void OnEditContextClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not CollectionItem item) return;

        var existing = new Collection { Id = item.Id, Name = item.Name, Description = item.Description };
        var dialog = new CollectionEditDialog(existing) { XamlRoot = XamlRoot };
        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
            await ViewModel.UpdateAsync(item.Id, dialog.CollectionName, dialog.CollectionDescription);
        else if (result == ContentDialogResult.Secondary)
            await ConfirmDeleteAsync(item);
    }

    private async void OnDeleteContextClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is CollectionItem item)
            await ConfirmDeleteAsync(item);
    }

    private async Task ConfirmDeleteAsync(CollectionItem item)
    {
        var confirm = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Delete collection?",
            Content = $"“{item.Name}” will be deleted. The games stay in your library.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };

        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.DeleteAsync(item.Id);
    }
}
