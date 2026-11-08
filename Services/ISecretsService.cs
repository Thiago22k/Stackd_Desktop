namespace Stackd.Services;

public interface ISecretsService
{
    string? TwitchClientId { get; }
    string? TwitchClientSecret { get; }
    bool HasTwitchCredentials { get; }

    string? SteamApiKey { get; }
    string? SteamId { get; }
    string? SteamPersonaName { get; }
    bool HasSteamLink { get; }

    Task LoadAsync();
    Task SaveTwitchCredentialsAsync(string clientId, string clientSecret);
    Task SaveSteamLinkAsync(string apiKey, string steamId, string? personaName);
    Task ClearSteamLinkAsync();
}
