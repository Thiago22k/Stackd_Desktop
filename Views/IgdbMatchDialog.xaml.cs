using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Stackd.Models;
using Stackd.Services;

namespace Stackd.Views;

public sealed partial class IgdbMatchDialog : ContentDialog
{
    private readonly IIgdbService _igdb;

    public IgdbGame? SelectedGame { get; private set; }

    public IgdbMatchDialog(IIgdbService igdb, string initialQuery)
    {
        _igdb = igdb;
        InitializeComponent();

        SearchBox.Text = initialQuery;
        IsPrimaryButtonEnabled = false;

        Loaded += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(initialQuery))
            {
                await SearchAsync(initialQuery);
            }
        };
    }

    private async void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await SearchAsync(SearchBox.Text);
        }
    }

    private async Task SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;

        LoadingRing.IsActive = true;
        LoadingRing.Visibility = Visibility.Visible;
        NoResultsText.Visibility = Visibility.Collapsed;
        ResultsList.ItemsSource = null;

        try
        {
            var results = await _igdb.SearchGamesAsync(query.Trim(), 15);
            ResultsList.ItemsSource = results;
            NoResultsText.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch
        {
            NoResultsText.Visibility = Visibility.Visible;
        }
        finally
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
        }
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedGame = ResultsList.SelectedItem as IgdbGame;
        IsPrimaryButtonEnabled = SelectedGame is not null;
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        SelectedGame = ResultsList.SelectedItem as IgdbGame;
        if (SelectedGame is null)
        {
            args.Cancel = true;
        }
    }
}

