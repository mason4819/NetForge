using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NetForge.Helpers;
using NetForge.ViewModels;

namespace NetForge.Views;

public sealed partial class DnsPage : Page
{
    public DnsViewModel Vm { get; } = new();
    public DnsPage() { InitializeComponent(); }
    private async void Apply_Click(object s, RoutedEventArgs e) => await Vm.ApplyAsync();
    private async void Backup_Click(object s, RoutedEventArgs e) => await Vm.BackupAsync();
    private async void Restore_Click(object s, RoutedEventArgs e) => await Vm.RestoreAsync();
    private void Relaunch_Click(object s, RoutedEventArgs e) => Elevation.RestartAsAdmin();

    // "I can't decide" helper: asks what you mainly want, then filters by purpose. No rankings, no "best".
    private async void Help_Click(object s, RoutedEventArgs e)
    {
        var radios = new RadioButtons { SelectedIndex = 0 };
        foreach (var k in new[] { ("Purpose_General", "Everyday browsing"), ("Purpose_Privacy", "Privacy"), ("Purpose_Security", "Protection from malicious sites"), ("Purpose_Family", "Family filtering"), ("Purpose_Custom", "Custom") })
            radios.Items.Add(Loc.Get(k.Item1, k.Item2));
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = Loc.Get("Wizard_Question", "What do you mainly want?"), Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] });
        panel.Children.Add(radios);
        var dlg = new ContentDialog { XamlRoot = XamlRoot, Title = Loc.Get("Dns_Help/Content", "Help me pick"), Content = panel, PrimaryButtonText = Loc.Get("Show", "Show options"), CloseButtonText = Loc.Get("Cancel", "Cancel"), DefaultButton = ContentDialogButton.Primary };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary) Vm.ChooseByPurpose(Math.Max(0, radios.SelectedIndex));
    }
}
