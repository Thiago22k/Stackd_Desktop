using Microsoft.EntityFrameworkCore;
using Stackd.Data;
using Stackd.Models;

namespace Stackd.Services;

public interface IGameMetadataService
{
    /// <summary>True si hay juegos cuyos géneros todavía no se han buscado y se pueden buscar en IGDB.</summary>
    Task<bool> HasPendingGenresAsync(CancellationToken ct = default);

    /// <summary>
    /// Busca en IGDB los géneros de los juegos que aún no los tienen: por IgdbId y, si no lo tienen,
    /// por su AppId de Steam (en ese caso también se guarda el IgdbId). Devuelve cuántos juegos se actualizaron.
    /// </summary>
    Task<int> FillMissingGenresAsync(IProgress<string>? progress = null, CancellationToken ct = default);
}

public class GameMetadataService : IGameMetadataService
{
    private readonly IIgdbService _igdb;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public GameMetadataService(IIgdbService igdb, IDbContextFactory<AppDbContext> dbFactory)
    {
        _igdb = igdb;
        _dbFactory = dbFactory;
    }

    public async Task<bool> HasPendingGenresAsync(CancellationToken ct = default)
    {
        if (!_igdb.IsConfigured) return false;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Games.AnyAsync(g => g.Genres == null && (g.IgdbId != null || g.SteamAppId != null), ct);
    }

    public async Task<int> FillMissingGenresAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (!_igdb.IsConfigured) return 0;

        // Si ya hay una búsqueda en marcha (p. ej. tras sincronizar Steam) no se lanza otra.
        if (!await _lock.WaitAsync(0, ct)) return 0;

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var pending = await db.Games
                .Where(g => g.Genres == null && (g.IgdbId != null || g.SteamAppId != null))
                .ToListAsync(ct);

            if (pending.Count == 0) return 0;

            progress?.Report("Reading genres from IGDB…");
            var updated = 0;

            // 1) Juegos ya vinculados con IGDB
            var byIgdb = pending.Where(g => g.IgdbId.HasValue).ToList();
            if (byIgdb.Count > 0)
            {
                var genres = await _igdb.GetGenresByIdsAsync(byIgdb.Select(g => g.IgdbId!.Value).ToList(), ct);
                if (genres is not null)
                {
                    foreach (var game in byIgdb)
                    {
                        game.Genres = genres.TryGetValue(game.IgdbId!.Value, out var list)
                            ? Game.JoinGenres(list)
                            : string.Empty;
                        updated++;
                    }
                }
            }

            // 2) Juegos de Steam sin IgdbId: se buscan por AppId
            var bySteam = pending.Where(g => !g.IgdbId.HasValue && g.SteamAppId.HasValue).ToList();
            if (bySteam.Count > 0)
            {
                var matches = await _igdb.FindBySteamAppIdsAsync(bySteam.Select(g => g.SteamAppId!.Value).ToList(), ct);
                if (matches is not null)
                {
                    // No se asigna un IgdbId que ya use otro juego de la biblioteca.
                    var usedIgdbIds = (await db.Games.Where(g => g.IgdbId != null).Select(g => g.IgdbId!.Value).ToListAsync(ct))
                        .ToHashSet();

                    foreach (var game in bySteam)
                    {
                        if (matches.TryGetValue(game.SteamAppId!.Value, out var match))
                        {
                            if (usedIgdbIds.Add(match.IgdbId))
                                game.IgdbId = match.IgdbId;
                            game.Genres = Game.JoinGenres(match.Genres);
                        }
                        else
                        {
                            game.Genres = string.Empty;
                        }
                        updated++;
                    }
                }
            }

            await db.SaveChangesAsync(ct);
            return updated;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Sin red o IGDB no disponible: se reintentará la próxima vez (los géneros siguen en null).
            return 0;
        }
        finally
        {
            _lock.Release();
        }
    }
}
