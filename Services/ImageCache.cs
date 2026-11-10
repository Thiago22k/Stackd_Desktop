using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Stackd.Data;

namespace Stackd.Services;

/// <summary>
/// Caché de portadas en disco (%LOCALAPPDATA%\Stackd\cache\images).
/// <see cref="Resolve"/> es síncrona: si la imagen ya está en disco devuelve la ruta local; si no, devuelve la
/// URL remota y la descarga en segundo plano para que la próxima vez salga del disco.
/// </summary>
public static class ImageCache
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(60);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly ConcurrentDictionary<string, byte> InFlight = new();
    private static readonly SemaphoreSlim Gate = new(4);

    private static string Directory_ => Path.Combine(AppPaths.CacheDirectory, "images");

    public static Uri? Resolve(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        // ms-appx, file, etc.: no se cachea.
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return uri;

        try
        {
            var path = PathFor(url);
            if (File.Exists(path))
                return new Uri(path);

            if (InFlight.TryAdd(path, 0))
                _ = Task.Run(() => DownloadAsync(uri, path));
        }
        catch
        {
            // Si el disco falla, se usa la URL remota sin caché.
        }

        return uri;
    }

    private static async Task DownloadAsync(Uri uri, string path)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await Gate.WaitAsync();
        try
        {
            System.IO.Directory.CreateDirectory(Directory_);

            using var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode) return;

            // Solo imágenes: evita guardar páginas de error.
            if (response.Content.Headers.ContentType?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) != true)
                return;

            await using (var file = File.Create(temp))
                await response.Content.CopyToAsync(file);

            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            // Sin red o sin disco: se reintentará la próxima vez que se muestre.
        }
        finally
        {
            Gate.Release();
            InFlight.TryRemove(path, out _);
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    /// <summary>Borra las imágenes que llevan más de 60 días sin descargarse de nuevo y restos de descargas cortadas.</summary>
    public static void Cleanup()
    {
        try
        {
            if (!System.IO.Directory.Exists(Directory_)) return;

            var limit = DateTime.UtcNow - MaxAge;
            foreach (var file in System.IO.Directory.EnumerateFiles(Directory_))
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

    private static string PathFor(string url)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)));
        return Path.Combine(Directory_, hash + ".img");
    }
}
