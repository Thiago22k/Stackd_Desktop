using Stackd.Models;

namespace Stackd.Services;

public interface ISteamService
{
    bool IsLinked { get; }

    /// <summary>Los datos de Steam se piden al servidor de Stackd: el usuario no necesita API key.</summary>
    bool UsesServer { get; }

    /// <summary>
    /// "Sign in with Steam": abre el navegador, el usuario inicia sesión en Steam y se vincula su cuenta
    /// automáticamente. <paramref name="apiKey"/> solo se usa cuando no hay servidor.
    /// </summary>
    Task<SteamLinkResult> SignInWithSteamAsync(string? apiKey, CancellationToken ct = default);

    /// <summary>Vincula una cuenta a partir de su SteamID64, URL de perfil o nombre personalizado.</summary>
    Task<SteamLinkResult> LinkAsync(string? apiKey, string profileInput, CancellationToken ct = default);

    Task UnlinkAsync();

    /// <summary>
    /// Importa la biblioteca de Steam: juegos nuevos, horas, última vez jugado, portadas y logros.
    /// </summary>
    Task<SteamSyncResult> SyncAsync(IProgress<string>? progress = null, CancellationToken ct = default);
}
