using Stackd.Models;

namespace Stackd.Services;

public interface ICollectionService
{
    Task<IReadOnlyList<CollectionItem>> GetAllAsync();
    Task<Collection?> GetAsync(int id);

    /// <summary>Juegos de la colección, ordenados por título.</summary>
    Task<IReadOnlyList<Game>> GetGamesAsync(int collectionId);

    /// <summary>Juegos de la biblioteca que todavía no están en la colección.</summary>
    Task<IReadOnlyList<Game>> GetGamesNotInAsync(int collectionId);

    Task<int> CreateAsync(string name, string? description);
    Task UpdateAsync(int id, string name, string? description);
    Task DeleteAsync(int id);

    Task AddGamesAsync(int collectionId, IEnumerable<int> gameIds);
    Task RemoveGameAsync(int collectionId, int gameId);

    /// <summary>Ids de las colecciones a las que pertenece un juego.</summary>
    Task<HashSet<int>> GetMembershipAsync(int gameId);

    /// <summary>Deja al juego exactamente en las colecciones indicadas.</summary>
    Task SetMembershipAsync(int gameId, IEnumerable<int> collectionIds);
}
