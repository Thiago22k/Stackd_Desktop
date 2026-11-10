using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Stackd.Data;
using Stackd.Services;
using Stackd.ViewModels;

namespace Stackd;

public partial class App : Application
{
    private Window? _window;

    public static IHost AppHost { get; private set; } = null!;

    /// <summary>Ventana principal (necesaria, por ejemplo, para los selectores de archivos).</summary>
    public static Window? MainWindow { get; private set; }

    public static T GetService<T>() where T : notnull => AppHost.Services.GetRequiredService<T>();

    public App()
    {
        InitializeComponent();

        AppHost = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                // Base de datos local (SQLite en %LOCALAPPDATA%\Stackd)
                services.AddDbContextFactory<AppDbContext>(o =>
                    o.UseSqlite($"Data Source={AppPaths.DatabaseFile}"));

                // Servicios
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<IGameLauncher, GameLauncher>();
                services.AddSingleton<ISecretsService, SecretsService>();
                services.AddSingleton<IIgdbService, IgdbService>();
                services.AddSingleton<ISteamService, SteamService>();
                services.AddSingleton<ICollectionService, CollectionService>();
                services.AddSingleton<IGameMetadataService, GameMetadataService>();

                // ViewModels
                services.AddSingleton<ShellViewModel>();
                services.AddTransient<HomeViewModel>();
                services.AddTransient<LibraryViewModel>();
                services.AddTransient<ExploreViewModel>();
                services.AddTransient<CollectionsViewModel>();
                services.AddTransient<CollectionDetailViewModel>();
                services.AddTransient<StatsViewModel>();

                // Ventanas
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        DbInitializer.Initialize(GetService<IDbContextFactory<AppDbContext>>());

        // Limpieza de cachés antiguas sin bloquear el arranque.
        _ = Task.Run(() =>
        {
            ResponseCache.Cleanup();
            ImageCache.Cleanup();
        });

        _window = GetService<MainWindow>();
        MainWindow = _window;
        _window.Activate();
    }
}
