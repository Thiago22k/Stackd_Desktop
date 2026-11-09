using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Stackd.Models;
using Stackd.Services;
using Windows.System;

namespace Stackd.Views;

/// <summary>Elegir en qué colecciones está un juego, con la opción de crear una nueva al momento.</summary>
public sealed partial class CollectionPickerDialog : ContentDialog
{
    private readonly ICollectionService _collections;
    private readonly Game _game;
    private readonly ObservableCollection<CollectionPickItem> _items = new();

    public CollectionPickerDialog(Game game, ICollectionService collections)
    {
        _game = game;
        _collections = collections;
        InitializeComponent();

        GameText.Text = game.Title;
        CollectionsList.ItemsSource = _items;
    }

    public IReadOnlyList<int> SelectedCollectionIds =>
        _items.Where(i => i.IsSelected).Select(i => i.Id).ToList();

    /// <summary>Carga las colecciones y marca aquellas a las que el juego ya pertenece.</summary>
    public async Task InitializeAsync()
    {
        var membership = await _collections.GetMembershipAsync(_game.Id);
        var all = await _collections.GetAllAsync();

        _items.Clear();
        foreach (var c in all)
            _items.Add(new CollectionPickItem(c.Id, c.Name, c.GameCount, membership.Contains(c.Id)));

        UpdateEmptyState();
    }

    private void UpdateEmptyState() =>
        EmptyText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private async void OnCreateClick(object sender, RoutedEventArgs e) => await CreateAsync();

    private async void OnNewNameKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            await CreateAsync();
        }
    }

    private async Task CreateAsync()
    {
        var name = NewNameBox.Text.Trim();
        if (name.Length == 0)
        {
            ErrorText.Text = "Type a name for the new collection.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        ErrorText.Visibility = Visibility.Collapsed;

        var id = await _collections.CreateAsync(name, null);

        // La nueva colección queda marcada: el usuario ya dijo que quiere el juego en ella.
        _items.Insert(0, new CollectionPickItem(id, name, 0, true));
        NewNameBox.Text = string.Empty;
        UpdateEmptyState();
    }
}
