using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Stackd.Models;
using Stackd.Services;

namespace Stackd.ViewModels;

#pragma warning disable MVVMTK0045
public partial class CollectionDetailViewModel : ObservableObject
{
    private readonly ICollectionService _collections;
    private readonly IGameLauncher _launcher;

    public CollectionDetailViewModel(ICollectionService collections, IGameLauncher launcher)
    {
        _collections = collections;
        _launcher = launcher;
    }

    public int CollectionId { get; private set; }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDescription))]
    private string? _description;

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public ObservableCollection<Game> Games { get; } = new();

    public bool ShowEmptyState => Games.Count == 0;

    public string CountLabel => Games.Count == 1 ? "1 game" : $"{Games.Count} games";

    /// <summary>False si la colección ya no existe (por ejemplo, se borró).</summary>
    public async Task<bool> LoadAsync(int collectionId)
    {
        CollectionId = collectionId;

        var collection = await _collections.GetAsync(collectionId);
        if (collection is null) return false;

        Name = collection.Name;
        Description = collection.Description;

        var games = await _collections.GetGamesAsync(collectionId);
        Games.Clear();
        foreach (var game in games) Games.Add(game);

        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(CountLabel));
        return true;
    }

    public Task<IReadOnlyList<Game>> GetCandidatesAsync() => _collections.GetGamesNotInAsync(CollectionId);

    public async Task AddGamesAsync(IEnumerable<int> gameIds)
    {
        await _collections.AddGamesAsync(CollectionId, gameIds);
        await LoadAsync(CollectionId);
    }

    public async Task RemoveGameAsync(int gameId)
    {
        await _collections.RemoveGameAsync(CollectionId, gameId);
        await LoadAsync(CollectionId);
    }

    public async Task UpdateAsync(string name, string? description)
    {
        await _collections.UpdateAsync(CollectionId, name, description);
        await LoadAsync(CollectionId);
    }

    public Task DeleteAsync() => _collections.DeleteAsync(CollectionId);

    public Task<string?> LaunchAsync(Game game) => _launcher.LaunchAsync(game.Id);
}
#pragma warning restore MVVMTK0045
