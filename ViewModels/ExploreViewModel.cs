using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Stackd.Data;
using Stackd.Models;
using Stackd.Services;

namespace Stackd.ViewModels;

#pragma warning disable MVVMTK0045
public partial class ExploreViewModel : ObservableObject
{
    private readonly IIgdbService _igdb;
    private readonly ISecretsService _secrets;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public ObservableCollection<IgdbGame> PopularGames { get; } = new();
    public ObservableCollection<IgdbGame> MostAnticipatedGames { get; } = new();
    public ObservableCollection<IgdbGame> SearchResults { get; } = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSearchState))]
    [NotifyPropertyChangedFor(nameof(ShowExploreSections))]
    private bool _isSearching;

    [ObservableProperty]
    private bool _hasCredentials;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasErrorMessage => !string.IsNullOrEmpty(ErrorMessage);
    public bool ShowSearchState => IsSearching && SearchResults.Count == 0 && !IsLoading;
    public bool ShowExploreSections => !IsSearching;

    public ExploreViewModel(IIgdbService igdb, ISecretsService secrets, IDbContextFactory<AppDbContext> dbFactory)
    {
        _igdb = igdb;
        _secrets = secrets;
        _dbFactory = dbFactory;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await _secrets.LoadAsync();
        HasCredentials = _secrets.HasIgdbAccess;

        if (!HasCredentials)
        {
            ErrorMessage = "IGDB credentials are not configured yet. Configure your Twitch Client ID and Secret to explore games.";
            OnPropertyChanged(nameof(HasErrorMessage));
            return;
        }

        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(HasErrorMessage));

        IsLoading = true;
        try
        {
            var popularTask = _igdb.GetPopularGamesAsync(16);
            var anticipatedTask = _igdb.GetMostAnticipatedAsync(16);

            await Task.WhenAll(popularTask, anticipatedTask);

            var popular = await popularTask;
            var anticipated = await anticipatedTask;

            await using var db = await _dbFactory.CreateDbContextAsync();
            var existingIgdbIds = (await db.Games
                .Where(g => g.IgdbId != null)
                .Select(g => g.IgdbId!.Value)
                .ToListAsync())
                .ToHashSet();

            var existingTitles = (await db.Games
                .Select(g => g.Title.ToLower())
                .ToListAsync())
                .ToHashSet();

            PopularGames.Clear();
            foreach (var g in popular)
            {
                g.IsInLibrary = existingIgdbIds.Contains(g.Id) || existingTitles.Contains(g.Name.ToLower());
                PopularGames.Add(g);
            }

            MostAnticipatedGames.Clear();
            foreach (var g in anticipated)
            {
                g.IsInLibrary = existingIgdbIds.Contains(g.Id) || existingTitles.Contains(g.Name.ToLower());
                MostAnticipatedGames.Add(g);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading games from IGDB: {ex.Message}";
            OnPropertyChanged(nameof(HasErrorMessage));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        var query = SearchText.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            IsSearching = false;
            SearchResults.Clear();
            OnPropertyChanged(nameof(ShowSearchState));
            return;
        }

        if (!HasCredentials) return;

        IsLoading = true;
        IsSearching = true;
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(HasErrorMessage));

        try
        {
            var results = await _igdb.SearchGamesAsync(query, 24);

            await using var db = await _dbFactory.CreateDbContextAsync();
            var existingIgdbIds = (await db.Games
                .Where(g => g.IgdbId != null)
                .Select(g => g.IgdbId!.Value)
                .ToListAsync())
                .ToHashSet();

            var existingTitles = (await db.Games
                .Select(g => g.Title.ToLower())
                .ToListAsync())
                .ToHashSet();

            SearchResults.Clear();
            foreach (var g in results)
            {
                g.IsInLibrary = existingIgdbIds.Contains(g.Id) || existingTitles.Contains(g.Name.ToLower());
                SearchResults.Add(g);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error searching IGDB: {ex.Message}";
            OnPropertyChanged(nameof(HasErrorMessage));
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(ShowSearchState));
        }
    }

    [RelayCommand]
    public void ClearSearch()
    {
        SearchText = string.Empty;
        IsSearching = false;
        SearchResults.Clear();
        OnPropertyChanged(nameof(ShowSearchState));
    }

    public async Task<bool> AddToLibraryAsync(IgdbGame igdbGame, PlayStatus initialStatus = PlayStatus.Backlog)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var alreadyExists = await db.Games.AnyAsync(g =>
                g.IgdbId == igdbGame.Id ||
                g.Title.ToLower() == igdbGame.Name.ToLower());

            if (alreadyExists)
            {
                igdbGame.IsInLibrary = true;
                return false;
            }

            var game = new Game
            {
                Title = igdbGame.Name,
                IgdbId = igdbGame.Id,
                CoverUrl = igdbGame.CoverUrl,
                Notes = igdbGame.Summary,
                Status = initialStatus,
                AddedAtUtc = DateTime.UtcNow
            };

            db.Games.Add(game);
            await db.SaveChangesAsync();

            igdbGame.IsInLibrary = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task SaveCredentialsAsync(string clientId, string clientSecret)
    {
        await _secrets.SaveTwitchCredentialsAsync(clientId, clientSecret);
        await LoadAsync();
    }
}
#pragma warning restore MVVMTK0045
