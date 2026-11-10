using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using Stackd.Services;

namespace Stackd.Converters;

/// <summary>Convierte la URL de una portada en un BitmapImage que sale de la caché en disco cuando existe.</summary>
public sealed class CachedImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        var uri = ImageCache.Resolve(value as string);
        return uri is null ? null : new BitmapImage(uri);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
