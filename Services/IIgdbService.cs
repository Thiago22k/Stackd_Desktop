using Stackd.Models;

namespace Stackd.Services;

public interface IIgdbService
{
    bool IsConfigured { get; }

    Task<IReadOnlyList<IgdbGame>> SearchGamesAsync(string query, int limit = 24, CancellationToken ct = default);
    Task<IReadOnlyList<IgdbGame>> GetPopularGamesAsync(int limit = 18, CancellationToken ct = default);
    Task<IReadOnlyList<IgdbGame>> GetMostAnticipatedAsync(int limit = 18, CancellationToken ct = default);
    Task<IgdbGame?> GetGameByIdAsync(long igdbId, CancellationToken ct = default);
}

