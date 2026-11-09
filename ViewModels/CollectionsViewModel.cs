using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Stackd.Models;
using Stackd.Services;

namespace Stackd.ViewModels;

#pragma warning disable MVVMTK0045
public partial class CollectionsViewModel : ObservableObject
{
    private readonly ICollectionService _collections;

    public CollectionsViewModel(ICollectionService collections)
    {
        _collections = collections;
    }

    public ObservableCollection<CollectionItem> Collections { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _isLoaded;

    public bool ShowEmptyState => IsLoaded && Collections.Count == 0;

    public string CountLabel => Collections.Count == 1 ? "1 collection" : $"{Collections.Count} collections";

    public async Task LoadAsync()
    {
        var items = await _collections.GetAllAsync();

        Collections.Clear();
        foreach (var item in items) Collections.Add(item);

        IsLoaded = true;
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(CountLabel));
    }

    public async Task CreateAsync(string name, string? description)
    {
        await _collections.CreateAsync(name, description);
        await LoadAsync();
    }

    public async Task UpdateAsync(int id, string name, string? description)
    {
        await _collections.UpdateAsync(id, name, description);
        await LoadAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await _collections.DeleteAsync(id);
        await LoadAsync();
    }
}
#pragma warning restore MVVMTK0045
