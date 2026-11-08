namespace Stackd.Models;

public record SteamLinkResult(bool Success, string? Error = null, string? PersonaName = null);

public record SteamSyncResult(
    bool Success,
    string? Error = null,
    int Total = 0,
    int Added = 0,
    int Linked = 0,
    int Updated = 0,
    int WithAchievements = 0,
    bool RateLimited = false)
{
    public string Summary
    {
        get
        {
            var text = $"Synced {Total} games: {Added} new, {Linked} matched with your library, {Updated} updated.";
            if (RateLimited)
                text += " Steam limited the requests, so some achievements were skipped. Try again in a few minutes.";
            return text;
        }
    }

    public static SteamSyncResult Failed(string error) => new(false, error);
}
