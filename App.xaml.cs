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

                // ViewModels
                services.AddSingleton<ShellViewModel>();
                services.AddTransient<HomeViewModel>();
                services.AddTransient<LibraryViewModel>();
                services.AddTransient<ExploreViewModel>();
                services.AddTransient<CollectionsViewModel>();
                services.AddTransient<CollectionDetailViewModel>();

                // Ventanas
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        DbInitializer.Initialize(GetService<IDbContextFactory<AppDbContext>>());

        _window = GetService<MainWindow>();
        MainWindow = _window;
        _window.Activate();
    }
}
