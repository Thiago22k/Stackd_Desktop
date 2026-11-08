namespace Stackd.Models;

/// <summary>Lista personalizada de juegos (pestaña "Collections").</summary>
public class Collection
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<Game> Games { get; set; } = new();
}
