using Microsoft.UI.Xaml.Media;

namespace Stackd.Models;

/// <summary>Tarjeta con una cifra grande (arriba de la pantalla de estadísticas).</summary>
public record StatCard(string Title, string Value, string Caption);

/// <summary>Fila de "Library by status": etiqueta, cantidad y barra proporcional.</summary>
public record StatusRow(string Label, int Count, string CountLabel, double Percent, Brush Brush);

/// <summary>Fila con portada, título y barra (más jugados, progreso de logros).</summary>
public record RankedRow(int Rank, string Title, string CoverUrl, string ValueLabel, double Percent, Brush Brush);

/// <summary>Dato suelto: "etiqueta ... valor".</summary>
public record HighlightRow(string Label, string Value);
