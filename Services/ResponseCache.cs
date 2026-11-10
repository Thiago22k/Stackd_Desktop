using System.Security.Cryptography;
using System.Text;
using Stackd.Data;

namespace Stackd.Services;

/// <summary>Caché en disco de respuestas de texto (%LOCALAPPDATA%\Stackd\cache\igdb), con caducidad por entrada.</summary>
public static class ResponseCache
{
    private static string Dir => Path.Combine(AppPaths.CacheDirectory, "igdb");

    /// <summary>Devuelve la respuesta guardada si tiene menos de <paramref name="maxAge"/>; con null devuelve cualquiera (aunque haya caducado).</summary>
    public static string? TryGet(string key, TimeSpan? maxAge)
    {
        try
        {
            var path = PathFor(key);
            if (!File.Exists(path)) return null;

            if (maxAge is { } age && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > age)
                return null;

            return File.ReadAllText(path, Encoding.UTF8);
        }
        catch
        {
            return null;
        }
    }

    public static void Set(string key, string content)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var path = PathFor(key);
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temp, content, Encoding.UTF8);
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            // sin disco: simplemente no se cachea
        }
    }

    /// <summary>Borra las respuestas con más de 30 días y restos de escrituras cortadas.</summary>
    public static void Cleanup()
    {
        try
        {
            if (!Directory.Exists(Dir)) return;

            var limit = DateTime.UtcNow - TimeSpan.FromDays(30);
            foreach (var file in Directory.EnumerateFiles(Dir))
            {
                if (File.GetLastWriteTimeUtc(file) < limit || file.EndsWith(".tmp", StringComparison.Ordinal))
                    File.Delete(file);
            }
        }
        catch
        {
            // limpieza oportunista
        }
    }

    private static string PathFor(string key) =>
        Path.Combine(Dir, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".json");
}
