using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Stackd.Data;
using Stackd.Models;
using Windows.UI;

namespace Stackd.ViewModels;

#pragma warning disable MVVMTK0045
public partial class StatsViewModel : ObservableObject
{
    private const int TopCount = 8;

    // Colores del tema (Themes/Theme.xaml). Se repiten aquí porque las barras se crean desde código.
    private static readonly Brush AccentBar = Make(0xFF, 0xDE, 0x59);
    private static readonly Brush VioletBar = Make(0x8B, 0x7C, 0xF6);
    private static readonly Brush SuccessBar = Make(0x5F, 0xC9, 0xA0);
    private static readonly Brush DangerBar = Make(0xEF, 0x6B, 0x7B);
    private static readonly Brush InfoBar = Make(0x6C, 0xB4, 0xF0);

    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public StatsViewModel(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public ObservableCollection<StatCard> Cards { get; } = new();
    public ObservableCollection<StatusRow> StatusRows { get; } = new();
    public ObservableCollection<RankedRow> MostPlayed { get; } = new();
    public ObservableCollection<RankedRow> AchievementProgress { get; } = new();
    public ObservableCollection<HighlightRow> Highlights { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _hasGames;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _isLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoPlaytime))]
    private bool _hasPlayedGames;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoAchievements))]
    private bool _hasAchievements;

    public bool ShowNoPlaytime => !HasPlayedGames;
    public bool ShowNoAchievements => !HasAchievements;

    public bool ShowEmptyState => IsLoaded && !HasGames;

    public async Task LoadAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var games = await db.Games.AsNoTracking().ToListAsync();

        Cards.Clear();
        StatusRows.Clear();
        MostPlayed.Clear();
        AchievementProgress.Clear();
        Highlights.Clear();

        HasGames = games.Count > 0;
        IsLoaded = true;
        if (games.Count == 0) return;

        var owned = games.Where(g => g.Status != PlayStatus.Wishlist).ToList();
        var wishlist = games.Count - owned.Count;
        var played = games.Where(g => g.PlaytimeMinutes > 0).ToList();
        var notStarted = owned.Count(g => g.PlaytimeMinutes <= 0);
        var completed = games.Count(g => g.Status == PlayStatus.Completed);
        var totalMinutes = games.Sum(g => (long)g.PlaytimeMinutes);

        var withAchievements = games.Where(g => g.AchievementsTotal > 0).ToList();
        var unlocked = withAchievements.Sum(g => g.AchievementsUnlocked);
        var achievementsTotal = withAchievements.Sum(g => g.AchievementsTotal);

        // ----- Tarjetas -----
        Cards.Add(new StatCard("TOTAL PLAYTIME", Hours(totalMinutes),
            played.Count == 1 ? "in 1 game" : $"across {played.Count} games"));

        Cards.Add(new StatCard("GAMES", owned.Count.ToString("N0"),
            wishlist > 0 ? $"+ {wishlist} on your wishlist" : $"{notStarted} not started yet"));

        Cards.Add(new StatCard("COMPLETED", completed.ToString("N0"),
            owned.Count > 0 ? $"{Percent(completed, owned.Count):0}% of your library" : "—"));

        Cards.Add(achievementsTotal > 0
            ? new StatCard("ACHIEVEMENTS", unlocked.ToString("N0"),
                $"of {achievementsTotal:N0} · {Percent(unlocked, achievementsTotal):0}%")
            : new StatCard("ACHIEVEMENTS", "—", "Sync with Steam to see them"));

        // ----- Por estado -----
        foreach (var status in Enum.GetValues<PlayStatus>())
        {
            var count = games.Count(g => g.Status == status);
            StatusRows.Add(new StatusRow(status.ToString(), count, count.ToString("N0"),
                Percent(count, games.Count), StatusBrush(status)));
        }

        // ----- Más jugados -----
        var top = played.OrderByDescending(g => g.PlaytimeMinutes).Take(TopCount).ToList();
        var topMax = top.Count > 0 ? top[0].PlaytimeMinutes : 1;
        for (var i = 0; i < top.Count; i++)
        {
            MostPlayed.Add(new RankedRow(i + 1, top[i].Title, top[i].CoverImageUrl,
                Hours(top[i].PlaytimeMinutes), Percent(top[i].PlaytimeMinutes, topMax), AccentBar));
        }
        HasPlayedGames = MostPlayed.Count > 0;

        // ----- Progreso de logros -----
        var byProgress = withAchievements
            .OrderByDescending(g => (double)g.AchievementsUnlocked / g.AchievementsTotal)
            .ThenByDescending(g => g.AchievementsTotal)
            .Take(TopCount)
            .ToList();

        for (var i = 0; i < byProgress.Count; i++)
        {
            var g = byProgress[i];
            var pct = Percent(g.AchievementsUnlocked, g.AchievementsTotal);
            AchievementProgress.Add(new RankedRow(i + 1, g.Title, g.CoverImageUrl,
                $"{g.AchievementsLabel} · {pct:0}%", pct, pct >= 100 ? SuccessBar : VioletBar));
        }
        HasAchievements = AchievementProgress.Count > 0;

        // ----- Datos sueltos -----
        var perfect = withAchievements.Count(g => g.AchievementsUnlocked >= g.AchievementsTotal);
        var lastPlayed = games.Where(g => g.LastPlayedAtUtc != null).MaxBy(g => g.LastPlayedAtUtc);
        var onSteam = games.Count(g => g.IsSteam);

        Highlights.Add(new HighlightRow("Not started yet", notStarted.ToString("N0")));
        Highlights.Add(new HighlightRow("Average per played game",
            played.Count > 0 ? Hours((long)Math.Round(played.Average(g => g.PlaytimeMinutes))) : "—"));
        Highlights.Add(new HighlightRow("Perfect games (100% achievements)", perfect.ToString("N0")));
        Highlights.Add(new HighlightRow("Last played",
            lastPlayed is null ? "—" : $"{lastPlayed.Title} · {lastPlayed.LastPlayedAtUtc!.Value.ToLocalTime():d MMM}"));
        Highlights.Add(new HighlightRow("Source", $"{onSteam} on Steam · {games.Count - onSteam} added manually"));
    }

    private static Brush StatusBrush(PlayStatus status) => status switch
    {
        PlayStatus.Playing => AccentBar,
        PlayStatus.Completed => SuccessBar,
        PlayStatus.Backlog => VioletBar,
        PlayStatus.Dropped => DangerBar,
        _ => InfoBar
    };

    private static double Percent(double value, double total) => total <= 0 ? 0 : value * 100.0 / total;

    private static string Hours(long minutes)
    {
        if (minutes < 60) return $"{minutes} min";
        var hours = minutes / 60.0;
        return hours < 100 ? $"{hours:0.#} h" : $"{hours:N0} h";
    }

    private static Brush Make(byte r, byte g, byte b) => new SolidColorBrush(Color.FromArgb(0xFF, r, g, b));
}
#pragma warning restore MVVMTK0045
