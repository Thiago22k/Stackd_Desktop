namespace Stackd.Services;

public interface ISecretsService
{
    string? TwitchClientId { get; }
    string? TwitchClientSecret { get; }
    bool HasTwitchCredentials { get; }

    Task LoadAsync();
    Task SaveTwitchCredentialsAsync(string clientId, string clientSecret);
}

