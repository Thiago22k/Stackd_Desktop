using Microsoft.EntityFrameworkCore;
using Stackd.Models;

namespace Stackd.Data;

public static class DbInitializer
{
    public static void Initialize(IDbContextFactory<AppDbContext> factory)
    {
        using var db = factory.CreateDbContext();

        // Fase 1-2: EnsureCreated + parches simples de columnas.
        // Cuando el esquema se estabilice pasamos a Migrations.
        db.Database.EnsureCreated();
        EnsureColumn(db, "Games", "ExecutablePath", "TEXT");
        EnsureColumn(db, "Games", "Genres", "TEXT");

#if DEBUG
        // Datos de ejemplo para ver la interfaz mientras no hay importación real.
        if (!db.Games.Any())
        {
            db.Games.AddRange(
                new Game
                {
                    Title = "Sample Game One",
                    Status = PlayStatus.Playing,
                    PlaytimeMinutes = 120,
                    LastPlayedAtUtc = DateTime.UtcNow.AddDays(-8),
                    AchievementsUnlocked = 2,
                    AchievementsTotal = 78
                },
                new Game
                {
                    Title = "Sample Game Two",
                    Status = PlayStatus.Playing,
                    PlaytimeMinutes = 300,
                    LastPlayedAtUtc = DateTime.UtcNow.AddDays(-9),
                    AchievementsUnlocked = 10,
                    AchievementsTotal = 50
                });
            db.SaveChanges();
        }
#endif
    }

    /// <summary>Añade una columna a una tabla existente si todavía no existe (BD creada en una versión anterior).</summary>
    private static void EnsureColumn(AppDbContext db, string table, string column, string sqlType)
    {
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed) connection.Open();

        try
        {
            var exists = false;
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA table_info({table});";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
            }

            if (!exists)
            {
                using var alter = connection.CreateCommand();
                alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {sqlType} NULL;";
                alter.ExecuteNonQuery();
            }
        }
        finally
        {
            if (wasClosed) connection.Close();
        }
    }
}
