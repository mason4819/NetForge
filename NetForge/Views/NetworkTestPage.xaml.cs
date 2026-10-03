using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NetForge.ViewModels;

namespace NetForge.Views;

public sealed partial class NetworkTestPage : Page, ICleanup
{
    public NetworkTestViewModel Vm { get; } = new();
    public NetworkTestPage() { InitializeComponent(); }
    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        var w = App.MainWindowInstance;
        if (w.PendingAutoRun == "networktest") { w.PendingAutoRun = null; await Vm.RunAsync(); }
    }
    private async void Run_Click(object s, RoutedEventArgs e) => await Vm.RunAsync();
    private void Stop_Click(object s, RoutedEventArgs e) => Vm.Cancel();
    private void SwitchDns_Click(object s, RoutedEventArgs e) => App.MainWindowInstance.NavigateTo("dns");
    public void Cleanup() => Vm.Cancel();
}
