namespace Stackd.Services;

public interface ISecretsService
{
    string? TwitchClientId { get; }
    string? TwitchClientSecret { get; }
    bool HasTwitchCredentials { get; }

    /// <summary>URL del servidor de Stackd (Cloudflare Worker), sin "/" final. Null si no hay servidor.</summary>
    string? ServerUrl { get; }
    bool HasServer { get; }

    /// <summary>IGDB se puede usar: con credenciales propias o a través del servidor.</summary>
    bool HasIgdbAccess { get; }

    string? SteamApiKey { get; }
    string? SteamId { get; }
    string? SteamPersonaName { get; }

    /// <summary>Hay una cuenta de Steam vinculada (la API key es opcional: con servidor no se necesita).</summary>
    bool HasSteamLink { get; }

    /// <summary>Última sincronización de Steam terminada con éxito (UTC). Null si nunca se ha hecho.</summary>
    DateTime? SteamLastSyncUtc { get; }

    Task LoadAsync();
    Task SaveTwitchCredentialsAsync(string clientId, string clientSecret);
    Task SaveSteamLinkAsync(string? apiKey, string steamId, string? personaName);
    Task SaveSteamLastSyncAsync(DateTime utc);
    Task ClearSteamLinkAsync();
}
