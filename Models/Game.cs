using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace Stackd.Models;

public class Game
{
    public int Id { get; set; }

    // Identificadores externos (se llenan en fases posteriores)
    public long? IgdbId { get; set; }
    public long? SteamAppId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public string? Notes { get; set; }

    /// <summary>Ruta al .exe del juego (para "Continue playing").</summary>
    public string? ExecutablePath { get; set; }

    public PlayStatus Status { get; set; } = PlayStatus.Backlog;
    public int PlaytimeMinutes { get; set; }
    public DateTime? LastPlayedAtUtc { get; set; }
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;

    public int AchievementsUnlocked { get; set; }
    public int AchievementsTotal { get; set; }

    /// <summary>
    /// Géneros de IGDB separados por <see cref="GenreSeparator"/>.
    /// null = todavía no se han buscado; "" = se buscaron y IGDB no tiene ninguno (no se vuelve a pedir).
    /// </summary>
    public string? Genres { get; set; }

    public List<Collection> Collections { get; set; } = new();

    public const char GenreSeparator = '|';

    public static string JoinGenres(IEnumerable<string> genres) =>
        string.Join(GenreSeparator, genres
            .Select(g => g.Replace(GenreSeparator, '/').Trim())
            .Where(g => g.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase));

    [NotMapped]
    public IReadOnlyList<string> GenreList =>
        string.IsNullOrEmpty(Genres)
            ? Array.Empty<string>()
            : Genres.Split(GenreSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // ----- Propiedades de presentación (no se guardan en la BD) -----
    [NotMapped]
    public string PlaytimeLabel
    {
        get
        {
            if (PlaytimeMinutes <= 0) return "0 min played";
            var hours = PlaytimeMinutes / 60;
            var mins = PlaytimeMinutes % 60;

            if (hours == 0)
                return $"{mins} min played";

            if (mins == 0)
                return hours == 1 ? "1 hr played" : $"{hours} hrs played";

            return hours == 1 ? $"1 hr {mins} min played" : $"{hours} hrs {mins} min played";
        }
    }

    [NotMapped]
    public string LastPlayedLabel =>
        LastPlayedAtUtc is { } d
            ? $"Last played {d.ToLocalTime().ToString("d MMM", CultureInfo.InvariantCulture)}"
            : "Never played";

    [NotMapped]
    public string AchievementsLabel => $"{AchievementsUnlocked}/{AchievementsTotal}";

    /// <summary>El juego viene de (o está vinculado con) la biblioteca de Steam.</summary>
    [NotMapped]
    public bool IsSteam => SteamAppId.HasValue;

    [NotMapped]
    public bool HasExecutable => !string.IsNullOrWhiteSpace(ExecutablePath);

    /// <summary>Se puede abrir desde Stackd: por su .exe o, si viene de Steam, mediante steam://.</summary>
    [NotMapped]
    public bool CanLaunch => HasExecutable || SteamAppId.HasValue;

    [NotMapped]
    public string StatusLabel => Status.ToString();

    [NotMapped]
    public string CoverImageUrl =>
        string.IsNullOrWhiteSpace(CoverUrl) ? "ms-appx:///Assets/no_cover.png" : CoverUrl;
}
