using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Stackd.Services;
using Stackd.ViewModels;

namespace Stackd;

public sealed partial class MainWindow : Window
{
    private readonly INavigationService _navigation;
    private bool _syncingSelection;

    public ShellViewModel ViewModel { get; }

    public MainWindow(ShellViewModel viewModel, INavigationService navigation)
    {
        ViewModel = viewModel;
        _navigation = navigation;

        InitializeComponent();

        Title = "Stackd";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1440, 860));

        _navigation.Initialize(ContentFrame);

        MainList.ItemsSource = ViewModel.MainItems;
        FooterList.ItemsSource = ViewModel.FooterItems;

        MainList.SelectedIndex = 0; // Home
    }

    private void OnNavSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection) return;
        if (e.AddedItems.FirstOrDefault() is not NavItem item) return;

        // Solo un elemento activo entre las dos listas.
        _syncingSelection = true;
        if (ReferenceEquals(sender, MainList)) FooterList.SelectedItem = null;
        else MainList.SelectedItem = null;
        _syncingSelection = false;

        _navigation.Navigate(item.PageType, item);
    }
}
