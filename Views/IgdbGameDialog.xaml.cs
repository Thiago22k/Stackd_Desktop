using System.Net.Http;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Stackd.Models;

namespace Stackd.Views;

public sealed partial class IgdbGameDialog : ContentDialog
{
    public IgdbGame Game { get; }
    public PlayStatus SelectedStatus => (PlayStatus)(StatusBox.SelectedItem ?? PlayStatus.Backlog);

    public IgdbGameDialog(IgdbGame game)
    {
        Game = game;
        InitializeComponent();

        Title = game.Name;
        TitleText.Text = game.Name;
        RatingText.Text = game.RatingLabel;
        YearText.Text = game.ReleaseYearLabel;
        GenresText.Text = game.GenresLabel;
        SummaryText.Text = string.IsNullOrWhiteSpace(game.Summary)
            ? (string.IsNullOrWhiteSpace(game.Storyline) ? "No summary available." : game.Storyline)
            : game.Summary;

        StatusBox.ItemsSource = Enum.GetValues<PlayStatus>();
        StatusBox.SelectedItem = PlayStatus.Backlog;

        if (game.IsInLibrary)
        {
            PrimaryButtonText = "Already in Library";
            IsPrimaryButtonEnabled = false;
        }

        if (!string.IsNullOrWhiteSpace(game.CoverUrl) && Uri.TryCreate(game.CoverUrl, UriKind.Absolute, out var uri))
        {
            CoverImage.Source = new BitmapImage(uri);
        }
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Se procesa en quien invoca
    }
}

