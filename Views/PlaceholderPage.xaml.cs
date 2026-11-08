using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Stackd.ViewModels;

namespace Stackd.Views;

public sealed partial class PlaceholderPage : Page
{
    public PlaceholderPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is NavItem item)
        {
            TitleText.Text = item.Title;
            DescriptionText.Text = item.Description;
        }
    }
}
