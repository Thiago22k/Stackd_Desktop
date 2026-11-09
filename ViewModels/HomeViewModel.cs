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
    private const string DefaultSyncStatus = "Not synced yet";
    private static readonly TimeSpan AutoSyncInterval = TimeSpan.FromHours(24);

    /// <summary>La comprobación de sincronización automática se hace una sola vez por sesión de la app.</summary>
    private static bool _autoSyncChecked;

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IGameLauncher _launcher;
    private readonly ISteamService _steam;
    private readonly ISecretsService _secrets;

    public HomeViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        IGameLauncher launcher,
        ISteamService steam,
        ISecretsService secrets)
    {
        _dbFactory = dbFactory;
        _launcher = launcher;
        _steam = steam;
        _secrets = secrets;
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
    [NotifyPropertyChangedFor(nameof(ShowLinkBanner))]
    private bool _isSteamBannerVisible = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLinkBanner))]
    [NotifyCanExecuteChangedFor(nameof(SyncSteamCommand))]
    private bool _isSteamLinked;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SteamTitle))]
    private string _steamPersonaName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SyncSteamCommand))]
    private bool _isSyncing;

    [ObservableProperty]
    private string _syncStatus = DefaultSyncStatus;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _hasRecentGames;

    public bool ShowLinkBanner => !IsSteamLinked && IsSteamBannerVisible;

    public string SteamTitle => string.IsNullOrWhiteSpace(SteamPersonaName)
        ? "STEAM ACCOUNT LINKED"
        : $"STEAM · {SteamPersonaName}";

    public bool ShowEmptyState => !HasRecentGames;

    public string Greeting => $"Hi, {UserName}!";

    public ObservableCollection<Game> RecentGames { get; } = new();

    partial void OnUserNameChanged(string value) => OnPropertyChanged(nameof(Greeting));

    [RelayCommand]
    private void DismissSteamBanner() => IsSteamBannerVisible = false;

    /// <summary>Relee secrets.json y actualiza el estado de la vinculación con Steam.</summary>
    public async Task RefreshSteamStateAsync()
    {
        await _secrets.LoadAsync();

        IsSteamLinked = _secrets.HasSteamLink;
        SteamPersonaName = _secrets.SteamPersonaName ?? string.Empty;
        UserName = IsSteamLinked && !string.IsNullOrWhiteSpace(SteamPersonaName) ? SteamPersonaName : "Guest";

        if (IsSteamLinked && !IsSyncing && SyncStatus == DefaultSyncStatus && _secrets.SteamLastSyncUtc is { } last)
            SyncStatus = $"Last synced {last.ToLocalTime():d MMM, HH:mm}";
    }

    /// <summary>Desvincula la cuenta. Los juegos ya importados se conservan.</summary>
    public async Task UnlinkSteamAsync()
    {
        await _steam.UnlinkAsync();
        SyncStatus = DefaultSyncStatus;
        await RefreshSteamStateAsync();
    }

    private bool CanSyncSteam() => IsSteamLinked && !IsSyncing;

    [RelayCommand(CanExecute = nameof(CanSyncSteam))]
    public async Task SyncSteamAsync()
    {
        IsSyncing = true;
        SyncStatus = "Starting…";

        try
        {
            // Progress<T> se crea en el hilo de la interfaz, así que las actualizaciones llegan a él.
            var progress = new Progress<string>(message => SyncStatus = message);
            var result = await _steam.SyncAsync(progress);

            SyncStatus = result.Success ? result.Summary : result.Error ?? "Sync failed.";
            if (result.Success) await LoadAsync();
        }
        catch (Exception ex)
        {
            SyncStatus = $"Sync failed: {ex.Message}";
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RefreshSteamStateAsync();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var games = await db.Games
            .Where(g => g.Status == PlayStatus.Playing || g.LastPlayedAtUtc != null)
            .OrderByDescending(g => g.LastPlayedAtUtc ?? g.AddedAtUtc)
            .Take(6)
            .ToListAsync();

        RecentGames.Clear();
        foreach (var g in games) RecentGames.Add(g);
        HasRecentGames = RecentGames.Count > 0;

        await TryAutoSyncAsync();
    }

    /// <summary>Sincroniza con Steam al abrir la app si la última sincronización tiene más de 24 horas.</summary>
    private async Task TryAutoSyncAsync()
    {
        if (_autoSyncChecked) return;
        _autoSyncChecked = true;

        if (!IsSteamLinked || IsSyncing) return;

        var last = _secrets.SteamLastSyncUtc;
        if (last is not null && DateTime.UtcNow - last.Value < AutoSyncInterval) return;

        if (SyncSteamCommand.CanExecute(null))
            await SyncSteamCommand.ExecuteAsync(null);
    }
}
#pragma warning restore MVVMTK0045
