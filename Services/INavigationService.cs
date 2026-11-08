using Microsoft.UI.Xaml.Controls;

namespace Stackd.Services;

public interface INavigationService
{
    void Initialize(Frame frame);
    bool Navigate(Type pageType, object? parameter = null);
}
