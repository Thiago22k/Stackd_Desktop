namespace Stackd.Models;

public class IgdbGame
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Storyline { get; set; }
    public string? Summary { get; set; }
    public string? CoverUrl { get; set; }
    public double? Rating { get; set; }
    public DateTime? FirstReleaseDate { get; set; }
    public List<string> Genres { get; set; } = new();
    public List<string> Platforms { get; set; } = new();
    public int? Hypes { get; set; }
    public bool IsInLibrary { get; set; }

    public string ReleaseYearLabel => FirstReleaseDate?.Year.ToString() ?? "TBA";
    public string RatingLabel => Rating.HasValue ? $"{Math.Round(Rating.Value)}%" : "N/A";
    public string GenresLabel => Genres.Count > 0 ? string.Join(", ", Genres.Take(2)) : "Game";
    public string CoverDisplayUrl => !string.IsNullOrWhiteSpace(CoverUrl) ? CoverUrl : "ms-appx:///Assets/no_cover.png";
}

