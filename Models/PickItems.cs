using CommunityToolkit.Mvvm.ComponentModel;

namespace Stackd.Models;

/// <summary>Juego con casilla de selección (para los cuadros de "añadir juegos").</summary>
public partial class GamePickItem : ObservableObject
{
    public GamePickItem(Game game) => Game = game;

    public Game Game { get; }
    public string Title => Game.Title;
    public string CoverImageUrl => Game.CoverImageUrl;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>Colección con casilla de selección (para el cuadro "Add to collection").</summary>
public partial class CollectionPickItem : ObservableObject
{
    public CollectionPickItem(int id, string name, int gameCount, bool isSelected)
    {
        Id = id;
        Name = name;
        GameCount = gameCount;
        _isSelected = isSelected;
    }

    public int Id { get; }
    public string Name { get; }
    public int GameCount { get; }
    public string CountLabel => GameCount == 1 ? "1 game" : $"{GameCount} games";

    [ObservableProperty]
    private bool _isSelected;
}
