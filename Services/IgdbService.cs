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

    public bool IsConfigured => _secrets.HasTwitchCredentials;

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

    private async Task<string?> QueryIgdbAsync(string apicalypseQuery, CancellationToken ct = default)
    {
        var token = await GetAccessTokenAsync(ct);
        if (token is null) return null;

        using var request = new HttpRequestMessage(HttpMethod.Post, IgdbBaseUrl);
        request.Headers.Add("Client-ID", _secrets.TwitchClientId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
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

        var exactTask = QueryIgdbAsync(exactApicalypse, ct);
        var searchTask = QueryIgdbAsync(searchApicalypse, ct);

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

        var json = await QueryIgdbAsync(apicalypse, ct);
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

        var json = await QueryIgdbAsync(apicalypse, ct);
        if (json is null) return Array.Empty<IgdbGame>();

        return ParseGames(json);
    }

    public async Task<IgdbGame?> GetGameByIdAsync(long igdbId, CancellationToken ct = default)
    {
        var apicalypse =
            $"fields id, name, summary, storyline, cover.image_id, cover.url, first_release_date, total_rating, genres.name, platforms.name; " +
            $"where id = {igdbId}; " +
            $"limit 1;";

        var json = await QueryIgdbAsync(apicalypse, ct);
        if (json is null) return null;

        var list = ParseGames(json);
        return list.Count > 0 ? list[0] : null;
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

