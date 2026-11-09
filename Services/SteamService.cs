using System.Net;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Stackd.Data;
using Stackd.Models;

namespace Stackd.Services;

public class SteamService : ISteamService
{
    private const string ApiBase = "https://api.steampowered.com";
    private const string CdnBase = "https://cdn.cloudflare.steamstatic.com/steam/apps";

    private readonly ISecretsService _secrets;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public SteamService(ISecretsService secrets, IDbContextFactory<AppDbContext> dbFactory)
    {
        _secrets = secrets;
        _dbFactory = dbFactory;
    }

    public bool IsLinked => _secrets.HasSteamLink;

    // ------------------------------------------------------------------
    // Vinculación
    // ------------------------------------------------------------------

    /// <summary>True si los datos de Steam se piden al servidor de Stackd (sin API key del usuario).</summary>
    public bool UsesServer => _secrets.HasServer;

    /// <summary>
    /// Construye la URL de una llamada a Steam. Con servidor, va al Worker (que añade la key);
    /// sin servidor, directa a Steam con la key del usuario.
    /// </summary>
    private string BuildUrl(string path, string? apiKey, string query) =>
        _secrets.HasServer
            ? $"{_secrets.ServerUrl}/steam/{path}/?{query}"
            : $"{ApiBase}/{path}/?key={apiKey}&{query}";

    private string KeyRejectedMessage => UsesServer
        ? "The Stackd server rejected the request. Try again later."
        : "Steam rejected the API key. Check that you copied it correctly.";

    public async Task<SteamLinkResult> SignInWithSteamAsync(string? apiKey, CancellationToken ct = default)
    {
        if (!UsesServer && string.IsNullOrWhiteSpace(apiKey) && string.IsNullOrWhiteSpace(_secrets.SteamApiKey))
            return new SteamLinkResult(false,
                "Enter your Steam Web API key under “Advanced” first, then sign in.");

        var (steamId, error) = await SteamOpenIdLogin.SignInAsync(_http, ct);
        if (steamId is null)
            return new SteamLinkResult(false, error ?? "Sign-in failed.");

        return await LinkAsync(apiKey, steamId, ct);
    }

    /// <summary>
    /// Valida el perfil (SteamID64, URL o nombre personalizado) y guarda la vinculación.
    /// La API key solo se usa sin servidor; si se deja vacía se reutiliza la guardada.
    /// </summary>
    public async Task<SteamLinkResult> LinkAsync(string? apiKey, string profileInput, CancellationToken ct = default)
    {
        apiKey = apiKey?.Trim();
        profileInput = profileInput.Trim();

        if (!UsesServer)
        {
            if (string.IsNullOrEmpty(apiKey)) apiKey = _secrets.SteamApiKey;

            if (string.IsNullOrEmpty(apiKey))
                return new SteamLinkResult(false, "A Steam Web API key is required.");

            if (!Regex.IsMatch(apiKey, "^[0-9A-Fa-f]{32}$"))
                return new SteamLinkResult(false, "The API key should be 32 characters (letters A-F and numbers).");
        }

        if (profileInput.Length == 0)
            return new SteamLinkResult(false, "Your Steam profile is required.");

        try
        {
            var (steamId, vanity) = ParseProfileInput(profileInput);

            if (steamId is null && vanity is null)
                return new SteamLinkResult(false, "Use your profile URL, your custom profile name or your 17-digit SteamID64.");

            if (steamId is null)
            {
                // El nombre personalizado solo se puede resolver con la key propia.
                if (UsesServer)
                    return new SteamLinkResult(false, "Use “Sign in with Steam” to link your account.");

                var (status, node) = await GetJsonAsync(
                    $"{ApiBase}/ISteamUser/ResolveVanityURL/v1/?key={apiKey}&vanityurl={Uri.EscapeDataString(vanity!)}", ct);

                if (status is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                    return new SteamLinkResult(false, KeyRejectedMessage);
                if (node is null)
                    return new SteamLinkResult(false, $"Steam returned an error ({(int)status}). Try again in a moment.");

                var response = node["response"];
                if ((response?["success"]?.GetValue<int>() ?? 0) != 1)
                    return new SteamLinkResult(false, "Couldn't find a Steam profile with that name.");

                steamId = response?["steamid"]?.GetValue<string>();
                if (steamId is null)
                    return new SteamLinkResult(false, "Couldn't find a Steam profile with that name.");
            }

            var (pStatus, pNode) = await GetJsonAsync(
                BuildUrl("ISteamUser/GetPlayerSummaries/v2", apiKey, $"steamids={steamId}"), ct);

            if (pStatus is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                return new SteamLinkResult(false, KeyRejectedMessage);
            if (pNode is null)
                return new SteamLinkResult(false, $"Steam returned an error ({(int)pStatus}). Try again in a moment.");

            var player = pNode["response"]?["players"]?.AsArray().FirstOrDefault();
            if (player is null)
                return new SteamLinkResult(false, "Couldn't find that Steam profile.");

            var visibility = player["communityvisibilitystate"]?.GetValue<int>() ?? 1;
            if (visibility != 3)
                return new SteamLinkResult(false,
                    "That profile is private. In Steam go to Profile → Edit Profile → Privacy Settings and set “My profile” and “Game details” to Public.");

            var persona = player["personaname"]?.GetValue<string>();

            await _secrets.SaveSteamLinkAsync(UsesServer ? null : apiKey, steamId, persona);
            return new SteamLinkResult(true, null, persona);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new SteamLinkResult(false, $"Couldn't reach Steam: {ex.Message}");
        }
    }

    public Task UnlinkAsync() => _secrets.ClearSteamLinkAsync();

    private static (string? SteamId, string? Vanity) ParseProfileInput(string input)
    {
        if (Regex.IsMatch(input, @"^\d{17}$"))
            return (input, null);

        var candidate = input;
        if (candidate.Contains("steamcommunity.com", StringComparison.OrdinalIgnoreCase) &&
            !candidate.Contains("://"))
            candidate = "https://" + candidate;

        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
            uri.Host.EndsWith("steamcommunity.com", StringComparison.OrdinalIgnoreCase))
        {
            var seg = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (seg.Length >= 2 && seg[0].Equals("profiles", StringComparison.OrdinalIgnoreCase) &&
                Regex.IsMatch(seg[1], @"^\d{17}$"))
                return (seg[1], null);
            if (seg.Length >= 2 && seg[0].Equals("id", StringComparison.OrdinalIgnoreCase))
                return (null, seg[1]);
            return (null, null);
        }

        if (Regex.IsMatch(input, @"^[A-Za-z0-9_\-]{2,32}$"))
            return (null, input);

        return (null, null);
    }

    // ------------------------------------------------------------------
    // Sincronización
    // ------------------------------------------------------------------

    private record OwnedGame(long AppId, string Name, int PlaytimeMinutes, DateTime? LastPlayedUtc);

    private record AchievementResult(bool RateLimited, int Unlocked, int Total);

    public async Task<SteamSyncResult> SyncAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (!IsLinked)
            return SteamSyncResult.Failed("Your Steam account is not linked yet.");

        if (!await _syncLock.WaitAsync(0, ct))
            return SteamSyncResult.Failed("A sync is already running.");

        try
        {
            var key = _secrets.SteamApiKey;
            var steamId = _secrets.SteamId!;

            if (!UsesServer && string.IsNullOrWhiteSpace(key))
                return SteamSyncResult.Failed(
                    "Your account is linked, but there is no Steam API key to read your library. Open Manage, add your key under “Advanced” and sign in again.");

            // 1) Biblioteca
            progress?.Report("Downloading your Steam library…");
            var (owned, error) = await GetOwnedGamesAsync(key, steamId, ct);
            if (owned is null)
                return SteamSyncResult.Failed(error ?? "Couldn't read your Steam library.");

            // 2) Cruce con la base de datos local
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var games = await db.Games.ToListAsync(ct);

            var byApp = new Dictionary<long, Game>();
            foreach (var g in games.Where(g => g.SteamAppId.HasValue))
                byApp.TryAdd(g.SteamAppId!.Value, g);

            var byTitle = new Dictionary<string, Game>();
            foreach (var g in games.Where(g => !g.SteamAppId.HasValue))
                byTitle.TryAdd(NormalizeTitle(g.Title), g);

            int added = 0, linked = 0, updated = 0;
            var touched = new List<(Game Game, OwnedGame Steam)>();

            foreach (var o in owned)
            {
                Game game;
                var changed = false;
                var isExisting = false;

                if (byApp.TryGetValue(o.AppId, out var existing))
                {
                    game = existing;
                    isExisting = true;
                }
                else if (byTitle.TryGetValue(NormalizeTitle(o.Name), out var manual))
                {
                    // Juego añadido a mano (o desde IGDB): se vincula con Steam sin perder su ejecutable ni sus notas.
                    game = manual;
                    byTitle.Remove(NormalizeTitle(o.Name));
                    game.SteamAppId = o.AppId;
                    if (game.Status == PlayStatus.Wishlist) game.Status = PlayStatus.Backlog;
                    linked++;
                    changed = true;
                }
                else
                {
                    game = new Game
                    {
                        Title = o.Name,
                        SteamAppId = o.AppId,
                        Status = PlayStatus.Backlog,
                        AddedAtUtc = DateTime.UtcNow
                    };
                    db.Games.Add(game);
                    added++;
                }

                // Las horas de Steam y las de Stackd cubren el mismo tiempo: se queda la mayor para no duplicar.
                if (o.PlaytimeMinutes > game.PlaytimeMinutes)
                {
                    game.PlaytimeMinutes = o.PlaytimeMinutes;
                    changed = true;
                }

                if (o.LastPlayedUtc is { } last && (game.LastPlayedAtUtc is null || last > game.LastPlayedAtUtc))
                {
                    game.LastPlayedAtUtc = last;
                    changed = true;
                }

                if (isExisting && changed)
                    updated++;

                touched.Add((game, o));
            }

            // 3) Portadas de Steam para los juegos que no tienen una
            var needCover = touched.Where(t => string.IsNullOrWhiteSpace(t.Game.CoverUrl)).ToList();
            if (needCover.Count > 0)
            {
                var done = 0;
                using var gate = new SemaphoreSlim(8);
                await Task.WhenAll(needCover.Select(async t =>
                {
                    await gate.WaitAsync(ct);
                    try
                    {
                        t.Game.CoverUrl = await FindCoverAsync(t.Steam.AppId, ct);
                    }
                    finally
                    {
                        gate.Release();
                        progress?.Report($"Checking covers… {Interlocked.Increment(ref done)}/{needCover.Count}");
                    }
                }));
            }

            // 4) Logros (solo juegos con horas: sin haber jugado no hay logros desbloqueados)
            var withHours = touched.Where(t => t.Steam.PlaytimeMinutes > 0).ToList();
            var achievementGames = 0;
            var rateLimited = false;

            if (withHours.Count > 0)
            {
                var done = 0;
                using var gate = new SemaphoreSlim(6);
                var results = await Task.WhenAll(withHours.Select(async t =>
                {
                    await gate.WaitAsync(ct);
                    try
                    {
                        if (rateLimited) return (t.Game, (AchievementResult?)null);
                        var r = await GetAchievementsAsync(key, steamId, t.Steam.AppId, ct);
                        if (r?.RateLimited == true) rateLimited = true;
                        return (t.Game, r);
                    }
                    finally
                    {
                        gate.Release();
                        progress?.Report($"Reading achievements… {Interlocked.Increment(ref done)}/{withHours.Count}");
                    }
                }));

                foreach (var (game, r) in results)
                {
                    if (r is null || r.RateLimited || r.Total <= 0) continue;

                    game.AchievementsUnlocked = r.Unlocked;
                    game.AchievementsTotal = r.Total;
                    achievementGames++;

                    if (r.Unlocked >= r.Total && game.Status is PlayStatus.Backlog or PlayStatus.Playing)
                        game.Status = PlayStatus.Completed;
                }
            }

            progress?.Report("Saving…");
            await db.SaveChangesAsync(ct);

            return new SteamSyncResult(true, null, owned.Count, added, linked, updated, achievementGames, rateLimited);
        }
        catch (OperationCanceledException)
        {
            return SteamSyncResult.Failed("Sync cancelled.");
        }
        catch (Exception ex)
        {
            return SteamSyncResult.Failed($"Couldn't sync with Steam: {ex.Message}");
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task<(List<OwnedGame>? Games, string? Error)> GetOwnedGamesAsync(string? key, string steamId, CancellationToken ct)
    {
        var url = BuildUrl("IPlayerService/GetOwnedGames/v1", key,
            $"steamid={steamId}&include_appinfo=1&include_played_free_games=1&format=json");

        var (status, node) = await GetJsonAsync(url, ct);

        if (status is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            return (null, KeyRejectedMessage);
        if (node is null)
            return (null, $"Steam returned an error ({(int)status}). Try again in a moment.");

        var response = node["response"];
        var gamesNode = response?["games"]?.AsArray();

        if (gamesNode is null)
        {
            return (null,
                "Steam returned no games. In Steam → Privacy Settings, set “Game details” to Public (and uncheck “Always keep my total playtime private”).");
        }

        var list = new List<OwnedGame>();
        foreach (var g in gamesNode)
        {
            if (g is null) continue;
            var appId = g["appid"]?.GetValue<long>();
            var name = g["name"]?.GetValue<string>();
            if (appId is null || string.IsNullOrWhiteSpace(name)) continue;

            var minutes = g["playtime_forever"]?.GetValue<int>() ?? 0;
            var lastUnix = g["rtime_last_played"]?.GetValue<long>() ?? 0;
            DateTime? last = lastUnix > 0 ? DateTimeOffset.FromUnixTimeSeconds(lastUnix).UtcDateTime : null;

            list.Add(new OwnedGame(appId.Value, name.Trim(), minutes, last));
        }

        return (list, null);
    }

    private async Task<AchievementResult?> GetAchievementsAsync(string? key, string steamId, long appId, CancellationToken ct)
    {
        try
        {
            var url = BuildUrl("ISteamUserStats/GetPlayerAchievements/v1", key, $"steamid={steamId}&appid={appId}");
            using var response = await _http.GetAsync(url, ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return new AchievementResult(true, 0, 0);

            // 400 = el juego no tiene logros (o no se pueden leer): se ignora.
            if (!response.IsSuccessStatusCode)
                return new AchievementResult(false, 0, 0);

            var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
            var list = node?["playerstats"]?["achievements"]?.AsArray();
            if (list is null) return new AchievementResult(false, 0, 0);

            var unlocked = list.Count(a => (a?["achieved"]?.GetValue<int>() ?? 0) == 1);
            return new AchievementResult(false, unlocked, list.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null; // error de red puntual: se conserva lo que ya hubiera
        }
    }

    /// <summary>
    /// Busca la mejor portada disponible en el CDN de Steam: vertical (600x900) y, si no existe, la cabecera.
    /// </summary>
    private async Task<string?> FindCoverAsync(long appId, CancellationToken ct)
    {
        var portrait = $"{CdnBase}/{appId}/library_600x900.jpg";
        var header = $"{CdnBase}/{appId}/header.jpg";

        try
        {
            if (await UrlExistsAsync(portrait, ct)) return portrait;
            if (await UrlExistsAsync(header, ct)) return header;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // sin red para esta imagen: se queda sin portada y se usa no_cover
        }

        return null;
    }

    private async Task<bool> UrlExistsAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, url);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        return response.IsSuccessStatusCode;
    }

    private async Task<(HttpStatusCode Status, JsonNode? Node)> GetJsonAsync(string url, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            return (response.StatusCode, null);

        var text = await response.Content.ReadAsStringAsync(ct);
        return (response.StatusCode, JsonNode.Parse(text));
    }

    private static string NormalizeTitle(string title)
    {
        var cleaned = title.Replace("™", "").Replace("®", "").Replace("©", "");
        return Regex.Replace(cleaned.ToLowerInvariant(), @"[^a-z0-9]", "");
    }
}
