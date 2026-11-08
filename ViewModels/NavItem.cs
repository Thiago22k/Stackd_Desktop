namespace Stackd.ViewModels;

/// <summary>Elemento de la barra lateral.</summary>
public record NavItem(string Title, string Glyph, Type PageType, string Description = "");
