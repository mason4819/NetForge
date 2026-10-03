using System.Net.NetworkInformation;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NetForge.Helpers;
using NetForge.Services;
using NetForge.ViewModels;

namespace NetForge.Views;

public sealed partial class AdvancedPage : Page
{
    public AdvancedViewModel Vm { get; private set; } = new("ipv4");
    public AdvancedPage() { InitializeComponent(); }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Vm = new AdvancedViewModel(e.Parameter as string ?? "ipv4");
        TitleText.Text = Vm.Title;
        TipText.Text = Vm.Tip; TipText.Visibility = Vm.HasTip ? Visibility.Visible : Visibility.Collapsed;
        Toolbar.Visibility = Vm.HasView ? Visibility.Visible : Visibility.Collapsed;
        OutputScroll.Visibility = Vm.HasView ? Visibility.Visible : Visibility.Collapsed;
        ActionList.Visibility = Vm.HasActions ? Visibility.Visible : Visibility.Collapsed;
        Bindings.Update();
        await Vm.LoadAsync();
    }

    private void Refresh_Click(object s, RoutedEventArgs e) => _ = Vm.LoadAsync();

    private async void Action_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not AdvAction act) return;
        var panel = new StackPanel { Spacing = 10, MinWidth = 440 };
        panel.Children.Add(new TextBlock { Text = Loc.Get("Confirm_Intro", "Please check before this runs:"), TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrEmpty(act.Warning))
            panel.Children.Add(new TextBlock { Text = "⚠ " + act.Warning, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold });

        var inputs = new Dictionary<string, Func<string>>();
        var preview = new TextBlock { FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), FontSize = 12, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, Opacity = 0.85 };
        void Refresh() => preview.Text = act.Build(inputs.ToDictionary(k => k.Key, k => k.Value()), false);

        foreach (var f in act.FieldList)
        {
            if (f.Kind is FieldKind.Adapter or FieldKind.Choice)
            {
                var names = f.Kind == FieldKind.Adapter
                    ? NetworkInterface.GetAllNetworkInterfaces().Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback).Select(n => n.Name).OrderBy(n => n).ToList()
                    : f.Options!.ToList();
                var cb = new ComboBox { Header = f.Label, ItemsSource = names, HorizontalAlignment = HorizontalAlignment.Stretch };
                cb.SelectedItem = names.Contains(f.Default) ? f.Default : names.FirstOrDefault();
                cb.SelectionChanged += (_, _) => Refresh();
                inputs[f.Key] = () => cb.SelectedItem as string ?? "";
                panel.Children.Add(cb);
            }
            else
            {
                var tb = new TextBox { Header = f.Label, Text = f.Default };
                tb.TextChanged += (_, _) => Refresh();
                inputs[f.Key] = () => tb.Text;
                panel.Children.Add(tb);
            }
        }
        panel.Children.Add(new TextBlock { Text = Loc.Get("Confirm_Will", "This will run:"), Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"] });
        panel.Children.Add(preview);
        if (!string.IsNullOrEmpty(act.Note)) panel.Children.Add(new TextBlock { Text = act.Note, TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });
        if (act.Admin && !Elevation.IsAdmin) panel.Children.Add(new TextBlock { Text = Loc.Get("Confirm_Uac", "Windows will ask for administrator approval just for this action."), TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });
        Refresh();

        var error = new TextBlock { Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"], TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        panel.Children.Add(error);
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = act.Label, Content = new ScrollViewer { Content = panel, MaxHeight = 520 },
            PrimaryButtonText = Loc.Get("Confirm_Run", "Run it"), CloseButtonText = Loc.Get("Cancel", "Cancel"), DefaultButton = ContentDialogButton.Close
        };
        dlg.PrimaryButtonClick += (_, args) =>
        {
            try { act.Build(inputs.ToDictionary(k => k.Key, k => k.Value()), true); }
            catch (FormatException ex) { args.Cancel = true; error.Text = Loc.Get("Confirm_Invalid", "Check this field:") + " " + ex.Message; error.Visibility = Visibility.Visible; }
        };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
            await Vm.RunAsync(act, inputs.ToDictionary(k => k.Key, k => k.Value()));
        ResultBar.Severity = Vm.ResultOk ? InfoBarSeverity.Success : InfoBarSeverity.Error;
    }
}
