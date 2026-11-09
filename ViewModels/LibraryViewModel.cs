using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Stackd.Data;
using Stackd.Models;
using Stackd.Services;

namespace Stackd.ViewModels;

#pragma warning disable MVVMTK0045
public partial class LibraryViewModel : ObservableObject
{
    public const string AllFilter = "All";
    public const string AllSources = "All sources";
    public const string SteamSource = "Steam";
    public const string NotSteamSource = "Not on Steam";

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IGameLauncher _launcher;
    private List<Game> _all = new();

    public LibraryViewModel(IDbContextFactory<AppDbContext> dbFactory, IGameLauncher launcher)
    {
        _dbFactory = dbFactory;
        _launcher = launcher;
        _launcher.GameExited += OnGameExited;

        StatusFilters = new[] { AllFilter }.Concat(Enum.GetNames<PlayStatus>()).ToList();
        SourceFilters = new[] { AllSources, SteamSource, NotSteamSource };
        SortOptions = new[]
        {
            "Title (A-Z)",
            "Title (Z-A)",
            "Most played",
            "Recently played"
        };
    }

    private void OnGameExited(object? sender, int gameId)
    {
        App.MainWindow?.DispatcherQueue.TryEnqueue(async () =>
        {
            await LoadAsync();
        });
    }

    public Task<string?> LaunchAsync(Game game) => _launcher.LaunchAsync(game.Id);

    public IReadOnlyList<string> StatusFilters { get; }
    public IReadOnlyList<string> SourceFilters { get; }
    public IReadOnlyList<string> SortOptions { get; }

    /// <summary>Juegos visibles (ya filtrados y ordenados).</summary>
    public ObservableCollection<Game> Games { get; } = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = AllFilter;

    [ObservableProperty]
    private string _selectedSource = AllSources;

    [ObservableProperty]
    private string _selectedSort = "Title (A-Z)";

    public bool ShowEmptyState => Games.Count == 0;

    public string CountLabel => _all.Count == 1 ? "1 game" : $"{_all.Count} games";

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedFilterChanged(string value) => ApplyFilter();

    partial void OnSelectedSourceChanged(string value) => ApplyFilter();

    partial void OnSelectedSortChanged(string value) => ApplyFilter();

    public async Task LoadAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        _all = await db.Games.AsNoTracking().ToListAsync();
        ApplyFilter();
    }

    public async Task SaveAsync(Game game)
    {
        if ((game.Status == PlayStatus.Playing || game.PlaytimeMinutes > 0) && game.LastPlayedAtUtc is null)
            game.LastPlayedAtUtc = DateTime.UtcNow;

        await using var db = await _dbFactory.CreateDbContextAsync();
        if (game.Id == 0) db.Games.Add(game);
        else db.Games.Update(game);
        await db.SaveChangesAsync();

        await LoadAsync();
    }

    public async Task DeleteAsync(int gameId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var game = await db.Games.FindAsync(gameId);
        if (game is not null)
        {
            db.Games.Remove(game);
            await db.SaveChangesAsync();
        }

        await LoadAsync();
    }

    private void ApplyFilter()
    {
        IEnumerable<Game> query = _all;

        if (SelectedFilter != AllFilter && Enum.TryParse<PlayStatus>(SelectedFilter, out var status))
            query = query.Where(g => g.Status == status);

        if (SelectedSource == SteamSource)
            query = query.Where(g => g.IsSteam);
        else if (SelectedSource == NotSteamSource)
            query = query.Where(g => !g.IsSteam);

        var text = SearchText.Trim();
        if (text.Length > 0)
            query = query.Where(g => g.Title.Contains(text, StringComparison.OrdinalIgnoreCase));

        query = SelectedSort switch
        {
            "Title (Z-A)" => query.OrderByDescending(g => g.Title),
            "Most played" => query.OrderByDescending(g => g.PlaytimeMinutes),
            "Recently played" => query.OrderByDescending(g => g.LastPlayedAtUtc ?? DateTime.MinValue),
            _ => query.OrderBy(g => g.Title)
        };

        Games.Clear();
        foreach (var g in query) Games.Add(g);

        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(CountLabel));
    }
}
#pragma warning restore MVVMTK0045
