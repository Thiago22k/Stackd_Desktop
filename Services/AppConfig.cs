namespace Stackd.Services;

/// <summary>
/// Configuración de la app que no es secreta.
/// </summary>
public static class AppConfig
{
    /// <summary>
    /// URL pública del servidor de Stackd (Cloudflare Worker), por ejemplo
    /// "https://stackd-api.tu-subdominio.workers.dev".
    ///
    /// Mientras esté vacía, la app funciona sin servidor: IGDB con las credenciales del usuario
    /// y Steam con su propia API key. Al rellenarla, Steam e IGDB pasan por el servidor y el usuario
    /// ya no necesita ninguna clave.
    ///
    /// Para probar sin recompilar se puede poner "ProxyUrl" en %LOCALAPPDATA%\Stackd\secrets.json.
    /// </summary>
    public const string DefaultServerUrl = "https://stackd-api.itzthiagok.workers.dev/";
}
