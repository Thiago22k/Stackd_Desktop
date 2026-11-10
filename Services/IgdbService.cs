using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Stackd.Models;

namespace Stackd.Services;

public class IgdbService : IIgdbService
{
    private const string TokenEndpoint = "https://id.twitch.tv/oauth2/token";
    private const string IgdbBaseUrl = "https://api.igdb.com/v4/games";

    private readonly ISecretsService _secrets;
    private readonly HttpClient _http;

    private string? _accessToken;
    private DateTime _tokenExpiresAtUtc = DateTime.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public bool IsConfigured => _secrets.HasIgdbAccess;

    public IgdbService(ISecretsService secrets)
    {
        _secrets = secrets;
        _http = new HttpClient();
    }

    private async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        if (!_secrets.HasTwitchCredentials) return null;

        if (_accessToken is not null && DateTime.UtcNow < _tokenExpiresAtUtc.AddMinutes(-5))
        {
            return _accessToken;
        }

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_accessToken is not null && DateTime.UtcNow < _tokenExpiresAtUtc.AddMinutes(-5))
            {
                return _accessToken;
            }

            var clientId = _secrets.TwitchClientId!;
            var clientSecret = _secrets.TwitchClientSecret!;

            var postData = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["grant_type"] = "client_credentials"
            };

            using var response = await _http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(postData), ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var node = JsonNode.Parse(json);
            if (node is null) return null;

            _accessToken = node["access_token"]?.GetValue<string>();
            var expiresIn = node["expires_in"]?.GetValue<int>() ?? 3600;
            _tokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn);

            return _accessToken;
        }
        catch
        {
            return null;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    // Cuánto tiempo se reutiliza una respuesta antes de volver a pedirla a IGDB.
    private static readonly TimeSpan ListTtl = TimeSpan.FromHours(6);      // Popular / Most anticipated
    private static readonly TimeSpan SearchTtl = TimeSpan.FromHours(12);
    private static readonly TimeSpan DetailTtl = TimeSpan.FromDays(7);     // un juego por id, géneros

    /// <summary>
    /// Consulta con caché en disco: si hay una respuesta reciente no se llama a IGDB; si la llamada falla
    /// (sin red, límite de peticiones…) se usa la última respuesta guardada aunque haya caducado.
    /// </summary>
    private async Task<string?> QueryIgdbAsync(string apicalypseQuery, TimeSpan ttl, CancellationToken ct = default)
    {
        var fresh = ResponseCache.TryGet(apicalypseQuery, ttl);
        if (fresh is not null) return fresh;

        string? json = null;
        try
        {
            json = await FetchIgdbAsync(apicalypseQuery, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            // se intenta con la caché caducada más abajo
        }

        if (json is not null)
        {
            ResponseCache.Set(apicalypseQuery, json);
            return json;
        }

        return ResponseCache.TryGet(apicalypseQuery, null);
    }

    private async Task<string?> FetchIgdbAsync(string apicalypseQuery, CancellationToken ct = default)
    {
        HttpRequestMessage request;

        if (_secrets.HasServer)
        {
            // El servidor de Stackd guarda el Client Secret, obtiene el token de Twitch y reenvía la consulta a IGDB.
            request = new HttpRequestMessage(HttpMethod.Post, $"{_secrets.ServerUrl}/igdb/v4/games");
        }
        else
        {
            var token = await GetAccessTokenAsync(ct);
            if (token is null) return null;

            request = new HttpRequestMessage(HttpMethod.Post, IgdbBaseUrl);
            request.Headers.Add("Client-ID", _secrets.TwitchClientId);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var _ = request;
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(apicalypseQuery, Encoding.UTF8, "text/plain");

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task<IReadOnlyList<IgdbGame>> SearchGamesAsync(string query, int limit = 24, CancellationToken ct = default)
    {
        var clean = query.Trim().Replace("\"", "\\\"");
        if (string.IsNullOrWhiteSpace(clean)) return Array.Empty<IgdbGame>();

        // 1. Búsqueda por coincidencia de nombre (ej: "How to Fish" para evitar que stop-words como 'to' o 'how' lo descarten)
        var exactApicalypse =
            $"fields id, name, summary, storyline, cover.image_id, cover.url, first_release_date, total_rating, genres.name, platforms.name; " +
            $"where name ~ *\"{clean}\"*; " +
            $"limit {limit};";

        // 2. Búsqueda semántica/fuzzy con el motor de IGDB
        var searchApicalypse =
            $"fields id, name, summary, storyline, cover.image_id, cover.url, first_release_date, total_rating, genres.name, platforms.name; " +
            $"search \"{clean}\"; " +
            $"limit {limit};";

        var exactTask = QueryIgdbAsync(exactApicalypse, SearchTtl, ct);
        var searchTask = QueryIgdbAsync(searchApicalypse, SearchTtl, ct);

        await Task.WhenAll(exactTask, searchTask);

        var exactJson = await exactTask;
        var searchJson = await searchTask;

        var exactGames = exactJson is not null ? ParseGames(exactJson) : new List<IgdbGame>();
        var searchGames = searchJson is not null ? ParseGames(searchJson) : new List<IgdbGame>();

        // Fusionar dando prioridad a las coincidencias de título
        var seenIds = new HashSet<long>();
        var combined = new List<IgdbGame>();

        foreach (var g in exactGames)
        {
            if (seenIds.Add(g.Id))
                combined.Add(g);
        }

        foreach (var g in searchGames)
        {
            if (seenIds.Add(g.Id))
                combined.Add(g);
        }

        return combined.Take(limit).ToList();
    }

    public async Task<IReadOnlyList<IgdbGame>> GetPopularGamesAsync(int limit = 18, CancellationToken ct = default)
    {
        var apicalypse =
            $"fields id, name, summary, storyline, cover.image_id, cover.url, first_release_date, total_rating, total_rating_count, genres.name, platforms.name; " +
            $"where cover != null & total_rating_count > 25 & first_release_date != null; " +
            $"sort total_rating desc; " +
            $"limit {limit};";

        var json = await QueryIgdbAsync(apicalypse, ListTtl, ct);
        if (json is null) return Array.Empty<IgdbGame>();

        return ParseGames(json);
    }

    public async Task<IReadOnlyList<IgdbGame>> GetMostAnticipatedAsync(int limit = 18, CancellationToken ct = default)
    {
        var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var apicalypse =
            $"fields id, name, summary, storyline, cover.image_id, cover.url, first_release_date, hypes, genres.name, platforms.name; " +
            $"where cover != null & first_release_date > {nowSeconds} & hypes != null; " +
            $"sort hypes desc; " +
            $"limit {limit};";

        var json = await QueryIgdbAsync(apicalypse, ListTtl, ct);
        if (json is null) return Array.Empty<IgdbGame>();

        return ParseGames(json);
    }

    public async Task<IgdbGame?> GetGameByIdAsync(long igdbId, CancellationToken ct = default)
    {
        var apicalypse =
            $"fields id, name, summary, storyline, cover.image_id, cover.url, first_release_date, total_rating, genres.name, platforms.name; " +
            $"where id = {igdbId}; " +
            $"limit 1;";

        var json = await QueryIgdbAsync(apicalypse, DetailTtl, ct);
        if (json is null) return null;

        var list = ParseGames(json);
        return list.Count > 0 ? list[0] : null;
    }

    // Lotes pequeños para no pasar del límite de 500 resultados de IGDB ni del tamaño de consulta del servidor.
    private const int LookupBatchSize = 150;

    // Origen "Steam" en external_games (external_game_source; antes "category").
    private const int SteamExternalSource = 1;

    public async Task<Dictionary<long, List<string>>?> GetGenresByIdsAsync(
        IReadOnlyCollection<long> igdbIds, CancellationToken ct = default)
    {
        var result = new Dictionary<long, List<string>>();

        foreach (var batch in igdbIds.Distinct().Chunk(LookupBatchSize))
        {
            var apicalypse =
                $"fields id, genres.name; " +
                $"where id = ({string.Join(',', batch)}); " +
                $"limit 500;";

            var json = await QueryIgdbAsync(apicalypse, DetailTtl, ct);
            if (json is null) return null;

            foreach (var g in ParseGames(json))
                result[g.Id] = g.Genres;
        }

        return result;
    }

    public async Task<Dictionary<long, IgdbSteamMatch>?> FindBySteamAppIdsAsync(
        IReadOnlyCollection<long> steamAppIds, CancellationToken ct = default)
    {
        var result = new Dictionary<long, IgdbSteamMatch>();

        foreach (var batch in steamAppIds.Distinct().Chunk(LookupBatchSize))
        {
            var wanted = batch.Select(id => id.ToString()).ToHashSet();
            var apicalypse =
                $"fields id, genres.name, external_games.*; " +
                $"where external_games.uid = ({string.Join(',', wanted.Select(u => $"\"{u}\""))}); " +
                $"limit 500;";

            var json = await QueryIgdbAsync(apicalypse, DetailTtl, ct);
            if (json is null) return null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;

                foreach (var elem in doc.RootElement.EnumerateArray())
                {
                    if (!elem.TryGetProperty("id", out var idProp) || !idProp.TryGetInt64(out var igdbId)) continue;
                    if (!elem.TryGetProperty("external_games", out var externals) ||
                        externals.ValueKind != JsonValueKind.Array) continue;

                    var genres = ReadNames(elem, "genres");

                    foreach (var ext in externals.EnumerateArray())
                    {
                        if (ext.ValueKind != JsonValueKind.Object) continue;
                        if (!ext.TryGetProperty("uid", out var uidProp) || uidProp.GetString() is not { } uid) continue;
                        if (!wanted.Contains(uid) || !IsSteamSource(ext)) continue;
                        if (!long.TryParse(uid, out var appId)) continue;

                        // Si hay varias entradas (ediciones, relanzamientos) se queda la primera con géneros.
                        if (!result.TryGetValue(appId, out var existing) || existing.Genres.Count == 0)
                            result[appId] = new IgdbSteamMatch(igdbId, genres);
                    }
                }
            }
            catch (JsonException)
            {
                // respuesta inesperada: se sigue con el siguiente lote
            }
        }

        return result;
    }

    private static bool IsSteamSource(JsonElement ext)
    {
        if (ext.TryGetProperty("external_game_source", out var src) && src.TryGetInt32(out var s))
            return s == SteamExternalSource;
        if (ext.TryGetProperty("category", out var cat) && cat.TryGetInt32(out var c))
            return c == SteamExternalSource;

        // Sin información de origen: se acepta solo si apunta a la tienda de Steam.
        return ext.TryGetProperty("url", out var url) &&
               url.GetString()?.Contains("store.steampowered.com", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static List<string> ReadNames(JsonElement elem, string property)
    {
        var names = new List<string>();
        if (elem.TryGetProperty(property, out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object &&
                    item.TryGetProperty("name", out var n) && n.GetString() is { } name)
                    names.Add(name);
            }
        }
        return names;
    }

    private static List<IgdbGame> ParseGames(string json)
    {
        var results = new List<IgdbGame>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return results;

            foreach (var elem in doc.RootElement.EnumerateArray())
            {
                var game = new IgdbGame();

                if (elem.TryGetProperty("id", out var idProp))
                    game.Id = idProp.GetInt64();

                if (elem.TryGetProperty("name", out var nameProp))
                    game.Name = nameProp.GetString() ?? string.Empty;

                if (elem.TryGetProperty("summary", out var sumProp))
                    game.Summary = sumProp.GetString();

                if (elem.TryGetProperty("storyline", out var storyProp))
                    game.Storyline = storyProp.GetString();

                if (elem.TryGetProperty("total_rating", out var ratingProp) && ratingProp.TryGetDouble(out var rating))
                    game.Rating = rating;

                if (elem.TryGetProperty("first_release_date", out var dateProp) && dateProp.TryGetInt64(out var unixDate))
                    game.FirstReleaseDate = DateTimeOffset.FromUnixTimeSeconds(unixDate).UtcDateTime;

                if (elem.TryGetProperty("hypes", out var hypesProp) && hypesProp.TryGetInt32(out var hypes))
                    game.Hypes = hypes;

                // Cover image
                if (elem.TryGetProperty("cover", out var coverElem) && coverElem.ValueKind == JsonValueKind.Object)
                {
                    if (coverElem.TryGetProperty("image_id", out var imgIdProp) && imgIdProp.GetString() is { } imgId)
                    {
                        game.CoverUrl = $"https://images.igdb.com/igdb/image/upload/t_cover_big/{imgId}.jpg";
                    }
                    else if (coverElem.TryGetProperty("url", out var urlProp) && urlProp.GetString() is { } url)
                    {
                        var full = url.StartsWith("//") ? "https:" + url : url;
                        game.CoverUrl = full.Replace("t_thumb", "t_cover_big");
                    }
                }

                // Genres
                if (elem.TryGetProperty("genres", out var genresElem) && genresElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var g in genresElem.EnumerateArray())
                    {
                        if (g.TryGetProperty("name", out var gName) && gName.GetString() is { } gn)
                            game.Genres.Add(gn);
                    }
                }

                // Platforms
                if (elem.TryGetProperty("platforms", out var platsElem) && platsElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in platsElem.EnumerateArray())
                    {
                        if (p.TryGetProperty("name", out var pName) && pName.GetString() is { } pn)
                            game.Platforms.Add(pn);
                    }
                }

                results.Add(game);
            }
        }
        catch
        {
            // Retorna lo obtenido
        }

        return results;
    }
}

