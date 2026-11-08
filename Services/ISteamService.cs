using Stackd.Models;

namespace Stackd.Services;

public interface ISteamService
{
    bool IsLinked { get; }

    /// <summary>
    /// Valida la API key y el perfil (URL, nombre personalizado o SteamID64) y guarda la vinculación.
    /// </summary>
    Task<SteamLinkResult> LinkAsync(string apiKey, string profileInput, CancellationToken ct = default);

    Task UnlinkAsync();

    /// <summary>
    /// Importa la biblioteca de Steam: juegos nuevos, horas, última vez jugado, portadas y logros.
    /// </summary>
    Task<SteamSyncResult> SyncAsync(IProgress<string>? progress = null, CancellationToken ct = default);
}
