using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;

namespace Stackd.Services;

public static class AppWindowManager
{
    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;
    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    /// <summary>
    /// Pasa Stackd a un proceso en segundo plano: oculta la ventana
    /// para detener el renderizado GPU y vacía el working set de memoria RAM.
    /// </summary>
    public static void EnterBackgroundMode()
    {
        if (App.MainWindow is not { } window) return;

        window.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                ShowWindow(hwnd, SW_HIDE);
            }
            catch
            {
                if (window.AppWindow?.Presenter is OverlappedPresenter presenter)
                {
                    presenter.Minimize();
                }
            }

            // Liberar memoria RAM del proceso para dejar el consumo al mínimo durante el juego
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                EmptyWorkingSet(Process.GetCurrentProcess().Handle);
            }
            catch
            {
                // Ignorar si no se tienen privilegios sobre el working set
            }
        });
    }

    /// <summary>
    /// Restaura la ventana de Stackd y la devuelve a primer plano cuando termina la sesión de juego.
    /// </summary>
    public static void RestoreFromBackgroundMode()
    {
        if (App.MainWindow is not { } window) return;

        window.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                ShowWindow(hwnd, SW_SHOW);
                ShowWindow(hwnd, SW_RESTORE);
                SetForegroundWindow(hwnd);
                window.Activate();
            }
            catch
            {
                if (window.AppWindow?.Presenter is OverlappedPresenter presenter)
                {
                    presenter.Restore();
                }
                window.Activate();
            }
        });
    }

    // Compatibilidad
    public static void Minimize() => EnterBackgroundMode();
    public static void Restore() => RestoreFromBackgroundMode();
}
