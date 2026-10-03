using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NetForge.Helpers;
using NetForge.ViewModels;

namespace NetForge.Views;

public sealed partial class AdaptersPage : Page
{
    public AdaptersViewModel Vm { get; private set; } = new(false);
    public AdaptersPage() { InitializeComponent(); }
    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        bool eth = e.Parameter as string == "ethernet";
        Vm = new AdaptersViewModel(eth);
        TitleText.Text = Loc.Get(eth ? "Nav_ethernet/Content" : "Nav_adapters/Content", eth ? "Ethernet" : "Adapters");
        Bindings.Update();
        await Vm.LoadAsync();
    }
    private async void Refresh_Click(object s, RoutedEventArgs e) => await Vm.LoadAsync();
}
