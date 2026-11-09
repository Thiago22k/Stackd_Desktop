using Microsoft.EntityFrameworkCore;
using Stackd.Data;
using Stackd.Models;

namespace Stackd.Services;

public class CollectionService : ICollectionService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public CollectionService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IReadOnlyList<CollectionItem>> GetAllAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var collections = await db.Collections.AsNoTracking().Include(c => c.Games).ToListAsync();

        return collections
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(CollectionItem.From)
            .ToList();
    }

    public async Task<Collection?> GetAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Collections.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<IReadOnlyList<Game>> GetGamesAsync(int collectionId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var games = await db.Games.AsNoTracking()
            .Where(g => g.Collections.Any(c => c.Id == collectionId))
            .ToListAsync();

        return games.OrderBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<IReadOnlyList<Game>> GetGamesNotInAsync(int collectionId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var games = await db.Games.AsNoTracking()
            .Where(g => !g.Collections.Any(c => c.Id == collectionId))
            .ToListAsync();

        return games.OrderBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<int> CreateAsync(string name, string? description)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var collection = new Collection
        {
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim()
        };

        db.Collections.Add(collection);
        await db.SaveChangesAsync();
        return collection.Id;
    }

    public async Task UpdateAsync(int id, string name, string? description)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var collection = await db.Collections.FindAsync(id);
        if (collection is null) return;

        collection.Name = name.Trim();
        collection.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        // Borra la colección y sus vínculos; los juegos se conservan en la biblioteca.
        await using var db = await _dbFactory.CreateDbContextAsync();
        var collection = await db.Collections.FindAsync(id);
        if (collection is null) return;

        db.Collections.Remove(collection);
        await db.SaveChangesAsync();
    }

    public async Task AddGamesAsync(int collectionId, IEnumerable<int> gameIds)
    {
        var ids = gameIds.Distinct().ToList();
        if (ids.Count == 0) return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var collection = await db.Collections.Include(c => c.Games).FirstOrDefaultAsync(c => c.Id == collectionId);
        if (collection is null) return;

        var existing = collection.Games.Select(g => g.Id).ToHashSet();
        var games = await db.Games.Where(g => ids.Contains(g.Id)).ToListAsync();

        foreach (var game in games.Where(g => !existing.Contains(g.Id)))
            collection.Games.Add(game);

        await db.SaveChangesAsync();
    }

    public async Task RemoveGameAsync(int collectionId, int gameId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var collection = await db.Collections.Include(c => c.Games).FirstOrDefaultAsync(c => c.Id == collectionId);
        if (collection is null) return;

        var game = collection.Games.FirstOrDefault(g => g.Id == gameId);
        if (game is null) return;

        collection.Games.Remove(game);
        await db.SaveChangesAsync();
    }

    public async Task<HashSet<int>> GetMembershipAsync(int gameId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var ids = await db.Collections
            .Where(c => c.Games.Any(g => g.Id == gameId))
            .Select(c => c.Id)
            .ToListAsync();

        return ids.ToHashSet();
    }

    public async Task SetMembershipAsync(int gameId, IEnumerable<int> collectionIds)
    {
        var wanted = collectionIds.ToHashSet();

        await using var db = await _dbFactory.CreateDbContextAsync();
        var game = await db.Games.Include(g => g.Collections).FirstOrDefaultAsync(g => g.Id == gameId);
        if (game is null) return;

        foreach (var collection in game.Collections.Where(c => !wanted.Contains(c.Id)).ToList())
            game.Collections.Remove(collection);

        var current = game.Collections.Select(c => c.Id).ToHashSet();
        var toAdd = await db.Collections.Where(c => wanted.Contains(c.Id)).ToListAsync();

        foreach (var collection in toAdd.Where(c => !current.Contains(c.Id)))
            game.Collections.Add(collection);

        await db.SaveChangesAsync();
    }
}
