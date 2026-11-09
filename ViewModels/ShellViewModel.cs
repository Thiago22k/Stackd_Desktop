using CommunityToolkit.Mvvm.ComponentModel;
using Stackd.Views;

namespace Stackd.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    // Iconos: Segoe Fluent Icons. Se pueden cambiar por los del diseño más adelante.
    public IReadOnlyList<NavItem> MainItems { get; } = new List<NavItem>
    {
        new("Home",        "", typeof(HomePage)),
        new("Library",     "", typeof(LibraryPage)),
        new("Explore",     "", typeof(ExplorePage)),
        new("Stats",       "", typeof(PlaceholderPage), "Estadísticas de tu tiempo de juego."),
        new("Collections", "", typeof(CollectionsPage)),
        new("Recommended", "", typeof(PlaceholderPage), "Recomendaciones profundas con IA."),
    };

    public IReadOnlyList<NavItem> FooterItems { get; } = new List<NavItem>
    {
        new("Hardware Profile", "", typeof(PlaceholderPage), "Especificaciones de tu PC y compatibilidad con juegos."),
        new("Settings",         "", typeof(PlaceholderPage), "Ajustes de la aplicación y cuentas vinculadas."),
    };
}
