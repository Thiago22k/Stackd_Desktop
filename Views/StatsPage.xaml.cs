using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Stackd.ViewModels;

namespace Stackd.Views;

public sealed partial class StatsPage : Page
{
    public StatsViewModel ViewModel { get; }

    public StatsPage()
    {
        ViewModel = App.GetService<StatsViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }
}
