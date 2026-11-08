using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Stackd.Data;
using Stackd.Models;

namespace Stackd.Services;

public class GameLauncher : IGameLauncher
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ConcurrentDictionary<int, Process> _runningProcesses = new();

    public event EventHandler<int>? GameExited;

    public GameLauncher(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public bool IsGameRunning(int gameId, string? executablePath = null)
    {
        // 1. Revisar si Stackd ya tiene registrado este juego en ejecución
        if (_runningProcesses.TryGetValue(gameId, out var runningProcess))
        {
            if (!runningProcess.HasExited)
            {
                return true;
            }

            _runningProcesses.TryRemove(gameId, out _);
        }

        // 2. Revisar si el proceso del ejecutable ya está corriendo en Windows
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            try
            {
                var processName = Path.GetFileNameWithoutExtension(executablePath);
                var existingProcesses = Process.GetProcessesByName(processName);
                if (existingProcesses.Length > 0)
                {
                    var fullTarget = Path.GetFullPath(executablePath);
                    foreach (var p in existingProcesses)
                    {
                        try
                        {
                            if (p.MainModule?.FileName is { } fn &&
                                string.Equals(Path.GetFullPath(fn), fullTarget, StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                        catch
                        {
                            // Si no se puede acceder a MainModule (permisos elevados o arquitectura),
                            // al coincidir el nombre del ejecutable se considera en ejecución para evitar duplicados.
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Ignorar excepciones al enumerar procesos
            }
        }

        return false;
    }

    public async Task<string?> LaunchAsync(int gameId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var game = await db.Games.FindAsync(gameId);

        if (game is null) return "Game not found.";
        if (string.IsNullOrWhiteSpace(game.ExecutablePath))
            return "This game has no executable yet. Open it in Library and select its .exe.";
        if (!File.Exists(game.ExecutablePath))
            return $"The file was not found:\n{game.ExecutablePath}";

        if (IsGameRunning(gameId, game.ExecutablePath))
        {
            return "The game is already running / El juego ya se encuentra abierto.";
        }

        Process? process;
        try
        {
            process = Process.Start(new ProcessStartInfo
            {
                FileName = game.ExecutablePath,
                WorkingDirectory = Path.GetDirectoryName(game.ExecutablePath) ?? string.Empty,
                UseShellExecute = true // permite juegos que piden permisos de administrador
            });
        }
        catch (Exception ex)
        {
            return $"Could not start the game: {ex.Message}";
        }

        if (process is null)
        {
            return "Failed to start process.";
        }

        _runningProcesses[gameId] = process;

        // Pasar Stackd a segundo plano inmediatamente para no consumir recursos
        AppWindowManager.EnterBackgroundMode();

        game.LastPlayedAtUtc = DateTime.UtcNow;
        if (game.Status is PlayStatus.Backlog or PlayStatus.Wishlist)
            game.Status = PlayStatus.Playing;
        await db.SaveChangesAsync();

        _ = TrackSessionAsync(gameId, process);

        return null;
    }

    /// <summary>Espera a que se cierre el juego, suma los minutos jugados y restaura la ventana.</summary>
    private async Task TrackSessionAsync(int gameId, Process process)
    {
        try
        {
            var started = DateTime.UtcNow;
            await process.WaitForExitAsync();
            var minutes = (int)Math.Round((DateTime.UtcNow - started).TotalMinutes);

            await using var db = await _dbFactory.CreateDbContextAsync();
            var game = await db.Games.FindAsync(gameId);
            if (game is not null)
            {
                game.PlaytimeMinutes += Math.Max(minutes, 0);
                game.LastPlayedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }
        catch
        {
            // Si no se puede seguir el proceso (permisos, etc.), simplemente no se suma tiempo.
        }
        finally
        {
            _runningProcesses.TryRemove(gameId, out _);
            process.Dispose();

            // Notificar a las vistas que el juego se cerró
            GameExited?.Invoke(this, gameId);

            // Restaurar la ventana de Stackd al terminar la partida
            AppWindowManager.RestoreFromBackgroundMode();
        }
    }
}
