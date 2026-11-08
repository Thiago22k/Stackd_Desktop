using Microsoft.EntityFrameworkCore;
using Stackd.Models;

namespace Stackd.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Game> Games => Set<Game>();
    public DbSet<Collection> Collections => Set<Collection>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Game>(e =>
        {
            e.Property(g => g.Title).IsRequired().HasMaxLength(300);
            e.Property(g => g.Status).HasConversion<string>();
            e.HasIndex(g => g.IgdbId);
            e.HasIndex(g => g.SteamAppId);
            // Relación muchos-a-muchos con Collection (tabla puente automática)
            e.HasMany(g => g.Collections).WithMany(c => c.Games);
        });

        b.Entity<Collection>(e =>
        {
            e.Property(c => c.Name).IsRequired().HasMaxLength(120);
        });
    }
}
