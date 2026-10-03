using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NetForge.ViewModels;

namespace NetForge.Views;

public sealed partial class SpeedTestPage : Page, ICleanup
{
    public SpeedViewModel Vm { get; } = new();
    public SpeedTestPage() { InitializeComponent(); }
    private async void Start_Click(object s, RoutedEventArgs e) => await Vm.RunAsync();
    private void Stop_Click(object s, RoutedEventArgs e) => Vm.Cancel();
    public void Cleanup() => Vm.Cancel();
}
