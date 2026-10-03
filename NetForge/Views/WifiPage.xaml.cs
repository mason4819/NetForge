using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NetForge.ViewModels;

namespace NetForge.Views;

public sealed partial class WifiPage : Page
{
    public WifiViewModel Vm { get; } = new();
    public WifiPage() { InitializeComponent(); Loaded += async (_, _) => await Vm.LoadAsync(); }
    private async void Refresh_Click(object s, RoutedEventArgs e) => await Vm.LoadAsync();
}
