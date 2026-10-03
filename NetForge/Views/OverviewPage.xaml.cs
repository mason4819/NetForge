using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NetForge.ViewModels;

namespace NetForge.Views;

public sealed partial class OverviewPage : Page
{
    public OverviewViewModel Vm { get; } = new();
    public OverviewPage() { InitializeComponent(); Loaded += async (_, _) => await Vm.LoadAsync(); }
    private async void Refresh_Click(object s, RoutedEventArgs e) => await Vm.LoadAsync();
    private void RunDiag_Click(object s, RoutedEventArgs e) => App.MainWindowInstance.NavigateTo("networktest", true);
    private void Quick_Click(object s, RoutedEventArgs e) => App.MainWindowInstance.NavigateTo((string)((FrameworkElement)s).Tag);
}
