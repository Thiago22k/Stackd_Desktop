using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Stackd.Data;

namespace Stackd.Services;

public class SecretsService : ISecretsService
{
    public string? TwitchClientId { get; private set; }
    public string? TwitchClientSecret { get; private set; }

    public bool HasTwitchCredentials =>
        !string.IsNullOrWhiteSpace(TwitchClientId) && !string.IsNullOrWhiteSpace(TwitchClientSecret);

    public string? ServerUrl { get; private set; } = NormalizeUrl(AppConfig.DefaultServerUrl);

    public bool HasServer => ServerUrl is not null;

    public bool HasIgdbAccess => HasServer || HasTwitchCredentials;

    public string? SteamApiKey { get; private set; }
    public string? SteamId { get; private set; }
    public string? SteamPersonaName { get; private set; }

    public bool HasSteamLink => !string.IsNullOrWhiteSpace(SteamId);

    public DateTime? SteamLastSyncUtc { get; private set; }

    private static string? NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim().TrimEnd('/');
        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? trimmed
            : null;
    }

    private static readonly string[] CandidatePaths = new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Stackd", "secrets.json"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Stackd", "secrets.json")
    };

    public async Task LoadAsync()
    {
        TwitchClientId = null;
        TwitchClientSecret = null;
        SteamApiKey = null;
        SteamId = null;
        SteamPersonaName = null;
        SteamLastSyncUtc = null;
        ServerUrl = NormalizeUrl(AppConfig.DefaultServerUrl);

        foreach (var path in CandidatePaths)
        {
            if (!File.Exists(path)) continue;

            try
            {
                var json = await File.ReadAllTextAsync(path);
                var node = JsonNode.Parse(json);
                if (node is null) continue;

                if (!HasTwitchCredentials)
                {
                    var twitch = node["Twitch"] ?? node["IGDB"] ?? node;

                    var id = (twitch["IgdbClientId"] ?? twitch["ClientId"] ?? twitch["Client_Id"] ?? twitch["client_id"] ?? twitch["TwitchClientId"])?.GetValue<string>()?.Trim();
                    var secret = (twitch["IgdbClientSecret"] ?? twitch["ClientSecret"] ?? twitch["Client_Secret"] ?? twitch["client_secret"] ?? twitch["TwitchClientSecret"])?.GetValue<string>()?.Trim();

                    if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(secret))
                    {
                        TwitchClientId = id;
                        TwitchClientSecret = secret;
                    }
                }

                if (!HasSteamLink)
                {
                    var steam = node["Steam"] ?? node;

                    var key = (steam["SteamApiKey"] ?? steam["ApiKey"])?.GetValue<string>()?.Trim();
                    var steamId = (steam["SteamId"] ?? steam["SteamID"])?.GetValue<string>()?.Trim();

                    // La API key es opcional: con el servidor de Stackd solo hace falta el SteamID.
                    if (!string.IsNullOrWhiteSpace(steamId))
                    {
                        SteamApiKey = string.IsNullOrWhiteSpace(key) ? null : key;
                        SteamId = steamId;
                        SteamPersonaName = steam["SteamPersonaName"]?.GetValue<string>()?.Trim();
                    }
                }

                if (SteamLastSyncUtc is null &&
                    node["SteamLastSyncUtc"]?.GetValue<string>() is { } lastSync &&
                    DateTime.TryParse(lastSync, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
                {
                    SteamLastSyncUtc = parsed.ToUniversalTime();
                }

                // Permite probar un servidor sin recompilar (anula AppConfig.DefaultServerUrl).
                if (node["ProxyUrl"]?.GetValue<string>() is { } proxyUrl && NormalizeUrl(proxyUrl) is { } normalized)
                    ServerUrl = normalized;

                if (HasTwitchCredentials && HasSteamLink && HasServer) return;
            }
            catch
            {
                // Continuar con el siguiente candidato
            }
        }
    }

    public async Task SaveTwitchCredentialsAsync(string clientId, string clientSecret)
    {
        TwitchClientId = clientId.Trim();
        TwitchClientSecret = clientSecret.Trim();

        await UpdateFileAsync(root =>
        {
            // Guardamos con compatibilidad amplia
            root["IgdbClientId"] = TwitchClientId;
            root["IgdbClientSecret"] = TwitchClientSecret;
        });
    }

    public async Task SaveSteamLinkAsync(string? apiKey, string steamId, string? personaName)
    {
        SteamApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        SteamId = steamId.Trim();
        SteamPersonaName = string.IsNullOrWhiteSpace(personaName) ? null : personaName.Trim();

        await UpdateFileAsync(root =>
        {
            root.Remove("Steam"); // por si el usuario lo había escrito anidado a mano
            if (SteamApiKey is null) root.Remove("SteamApiKey");
            else root["SteamApiKey"] = SteamApiKey;
            root["SteamId"] = SteamId;
            root["SteamPersonaName"] = SteamPersonaName;
        });
    }

    public async Task SaveSteamLastSyncAsync(DateTime utc)
    {
        SteamLastSyncUtc = utc;
        await UpdateFileAsync(root => root["SteamLastSyncUtc"] = utc.ToString("O", CultureInfo.InvariantCulture));
    }

    public async Task ClearSteamLinkAsync()
    {
        SteamLastSyncUtc = null;
        SteamApiKey = null;
        SteamId = null;
        SteamPersonaName = null;

        await UpdateFileAsync(root =>
        {
            root.Remove("Steam");
            root.Remove("SteamApiKey");
            root.Remove("ApiKey");
            root.Remove("SteamId");
            root.Remove("SteamID");
            root.Remove("SteamPersonaName");
            root.Remove("SteamLastSyncUtc");
        });
    }

    /// <summary>Lee secrets.json (si existe), aplica el cambio y lo vuelve a escribir.</summary>
    private static async Task UpdateFileAsync(Action<JsonObject> mutate)
    {
        // Si ya existe en alguno de los dos lugares, se actualiza allí; si no, en LocalApplicationData
        var targetPath = CandidatePaths.FirstOrDefault(File.Exists) ?? CandidatePaths[0];

        JsonObject root;
        if (File.Exists(targetPath))
        {
            try
            {
                var content = await File.ReadAllTextAsync(targetPath);
                root = JsonNode.Parse(content)?.AsObject() ?? new JsonObject();
            }
            catch
            {
                root = new JsonObject();
            }
        }
        else
        {
            root = new JsonObject();
        }

        mutate(root);

        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(targetPath, root.ToJsonString(options));
    }
}
