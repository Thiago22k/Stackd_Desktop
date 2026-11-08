using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Stackd.Data;
using Stackd.Models;
using Stackd.Services;

namespace Stackd.ViewModels;

#pragma warning disable MVVMTK0045
public partial class HomeViewModel : ObservableObject
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IGameLauncher _launcher;

    public HomeViewModel(IDbContextFactory<AppDbContext> dbFactory, IGameLauncher launcher)
    {
        _dbFactory = dbFactory;
        _launcher = launcher;
        _launcher.GameExited += OnGameExited;
    }

    private void OnGameExited(object? sender, int gameId)
    {
        App.MainWindow?.DispatcherQueue.TryEnqueue(async () =>
        {
            await LoadAsync();
        });
    }

    /// <summary>Abre el juego. Devuelve null si fue bien, o un mensaje de error.</summary>
    public Task<string?> LaunchAsync(Game game) => _launcher.LaunchAsync(game.Id);

    [ObservableProperty]
    private string _userName = "Guest";

    [ObservableProperty]
    private bool _isSteamBannerVisible = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _hasRecentGames;

    public bool ShowEmptyState => !HasRecentGames;

    public string Greeting => $"Hi, {UserName}!";

    public ObservableCollection<Game> RecentGames { get; } = new();

    partial void OnUserNameChanged(string value) => OnPropertyChanged(nameof(Greeting));

    [RelayCommand]
    private void DismissSteamBanner() => IsSteamBannerVisible = false;

    [RelayCommand]
    private void LinkSteam()
    {
        // Fase 5: iniciar el flujo de vinculación con Steam.
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var games = await db.Games
            .Where(g => g.Status == PlayStatus.Playing || g.LastPlayedAtUtc != null)
            .OrderByDescending(g => g.LastPlayedAtUtc ?? g.AddedAtUtc)
            .Take(6)
            .ToListAsync();

        RecentGames.Clear();
        foreach (var g in games) RecentGames.Add(g);
        HasRecentGames = RecentGames.Count > 0;
    }
}
#pragma warning restore MVVMTK0045
