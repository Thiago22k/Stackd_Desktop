namespace Stackd.Models;

/// <summary>Colección lista para mostrar en la cuadrícula (con conteo y mosaico de portadas).</summary>
public class CollectionItem
{
    private const string NoCover = "ms-appx:///Assets/no_cover.png";

    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int GameCount { get; init; }

    /// <summary>Cuatro portadas para el mosaico; si faltan, se rellena con no_cover.</summary>
    public IReadOnlyList<string> Covers { get; init; } = Array.Empty<string>();

    public string Cover1 => CoverAt(0);
    public string Cover2 => CoverAt(1);
    public string Cover3 => CoverAt(2);
    public string Cover4 => CoverAt(3);

    public bool HasGames => GameCount > 0;
    public bool IsEmpty => GameCount == 0;
    public string CountLabel => GameCount == 1 ? "1 game" : $"{GameCount} games";

    private string CoverAt(int index) => index < Covers.Count ? Covers[index] : NoCover;

    public static CollectionItem From(Collection collection) => new()
    {
        Id = collection.Id,
        Name = collection.Name,
        Description = collection.Description,
        GameCount = collection.Games.Count,
        Covers = collection.Games
            .OrderBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase)
            .Take(4)
            .Select(g => g.CoverImageUrl)
            .ToList()
    };
}
