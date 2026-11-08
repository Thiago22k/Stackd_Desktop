namespace Stackd.Services;

public interface IGameLauncher
{
    /// <summary>
    /// Abre el juego y empieza a contar el tiempo de la sesión.
    /// Devuelve null si todo fue bien, o un mensaje de error para mostrar al usuario.
    /// </summary>
    Task<string?> LaunchAsync(int gameId);

    /// <summary>
    /// Comprueba si el juego ya está en ejecución (sea lanzado por Stackd o externamente).
    /// </summary>
    bool IsGameRunning(int gameId, string? executablePath = null);

    /// <summary>
    /// Se dispara cuando el juego finaliza para que la UI pueda actualizar horas y estadísticas.
    /// </summary>
    event EventHandler<int>? GameExited;
}
