using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NetForge.ViewModels;

namespace NetForge.Views;

public sealed partial class TraceroutePage : Page, ICleanup
{
    public TraceViewModel Vm { get; } = new();
    public TraceroutePage() { InitializeComponent(); }
    private async void Start_Click(object s, RoutedEventArgs e) => await Vm.StartAsync();
    private void Stop_Click(object s, RoutedEventArgs e) => Vm.Cancel();
    public void Cleanup() => Vm.Cancel();
}
