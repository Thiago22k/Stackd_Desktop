using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Stackd.Models;

namespace Stackd.Views;

/// <summary>Elegir varios juegos de la biblioteca (por ejemplo, para añadirlos a una colección).</summary>
public sealed partial class GamePickerDialog : ContentDialog
{
    private readonly List<GamePickItem> _items;

    public GamePickerDialog(IEnumerable<Game> candidates)
    {
        _items = candidates.Select(g => new GamePickItem(g)).ToList();
        InitializeComponent();
        ApplyFilter();
    }

    public IReadOnlyList<int> SelectedGameIds =>
        _items.Where(i => i.IsSelected).Select(i => i.Game.Id).ToList();

    private void ApplyFilter()
    {
        var text = SearchBox.Text.Trim();
        var visible = _items
            .Where(i => text.Length == 0 || i.Title.Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // La selección vive en cada elemento, así que sobrevive al filtrar.
        GamesList.ItemsSource = visible;

        if (_items.Count == 0)
        {
            EmptyText.Text = "All the games in your library are already in this collection.";
            EmptyText.Visibility = Visibility.Visible;
        }
        else if (visible.Count == 0)
        {
            EmptyText.Text = "No games match your search.";
            EmptyText.Visibility = Visibility.Visible;
        }
        else
        {
            EmptyText.Visibility = Visibility.Collapsed;
        }

        UpdatePrimaryButton();
    }

    private void UpdatePrimaryButton()
    {
        var count = _items.Count(i => i.IsSelected);
        PrimaryButtonText = count == 0 ? "Add" : $"Add ({count})";
        IsPrimaryButtonEnabled = count > 0;
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void OnToggle(object sender, RoutedEventArgs e) => UpdatePrimaryButton();
}
