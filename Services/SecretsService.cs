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

    private static readonly string[] CandidatePaths = new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Stackd", "secrets.json"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Stackd", "secrets.json")
    };

    public async Task LoadAsync()
    {
        TwitchClientId = null;
        TwitchClientSecret = null;

        foreach (var path in CandidatePaths)
        {
            if (!File.Exists(path)) continue;

            try
            {
                var json = await File.ReadAllTextAsync(path);
                var node = JsonNode.Parse(json);
                if (node is null) continue;

                var twitch = node["Twitch"] ?? node["IGDB"] ?? node;

                var id = (twitch["IgdbClientId"] ?? twitch["ClientId"] ?? twitch["Client_Id"] ?? twitch["client_id"] ?? twitch["TwitchClientId"])?.GetValue<string>()?.Trim();
                var secret = (twitch["IgdbClientSecret"] ?? twitch["ClientSecret"] ?? twitch["Client_Secret"] ?? twitch["client_secret"] ?? twitch["TwitchClientSecret"])?.GetValue<string>()?.Trim();

                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(secret))
                {
                    TwitchClientId = id;
                    TwitchClientSecret = secret;
                    return;
                }
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

        // Guardamos con compatibilidad amplia
        root["IgdbClientId"] = TwitchClientId;
        root["IgdbClientSecret"] = TwitchClientSecret;

        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(targetPath, root.ToJsonString(options));
    }
}
