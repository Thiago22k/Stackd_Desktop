using Stackd.Models;

namespace Stackd.Services;

public interface IIgdbService
{
    bool IsConfigured { get; }

    Task<IReadOnlyList<IgdbGame>> SearchGamesAsync(string query, int limit = 24, CancellationToken ct = default);
    Task<IReadOnlyList<IgdbGame>> GetPopularGamesAsync(int limit = 18, CancellationToken ct = default);
    Task<IReadOnlyList<IgdbGame>> GetMostAnticipatedAsync(int limit = 18, CancellationToken ct = default);
    Task<IgdbGame?> GetGameByIdAsync(long igdbId, CancellationToken ct = default);

    /// <summary>Géneros de varios juegos de IGDB. Los ids sin respuesta no aparecen; null si falló la petición.</summary>
    Task<Dictionary<long, List<string>>?> GetGenresByIdsAsync(IReadOnlyCollection<long> igdbIds, CancellationToken ct = default);

    /// <summary>Busca en IGDB los juegos de Steam por su AppId. Clave: AppId. null si falló la petición.</summary>
    Task<Dictionary<long, IgdbSteamMatch>?> FindBySteamAppIdsAsync(IReadOnlyCollection<long> steamAppIds, CancellationToken ct = default);
}

public record IgdbSteamMatch(long IgdbId, List<string> Genres);

