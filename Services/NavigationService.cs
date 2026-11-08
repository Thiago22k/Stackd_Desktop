using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace Stackd.Services;

public class NavigationService : INavigationService
{
    private Frame? _frame;

    public void Initialize(Frame frame) => _frame = frame;

    public bool Navigate(Type pageType, object? parameter = null)
    {
        if (_frame is null) return false;
        if (_frame.CurrentSourcePageType == pageType && parameter is null) return false;

        return _frame.Navigate(pageType, parameter, new EntranceNavigationTransitionInfo());
    }
}
