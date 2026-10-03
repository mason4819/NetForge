using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using NetForge.Services;
using NetForge.Views;
using Windows.Graphics;

namespace NetForge;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "NetForge";
        try { AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "NetForge.ico")); AppWindow.Resize(new SizeInt32(1180, 800)); } catch { }
        ApplyTheme(); ApplyPerformance();
        Closed += (_, _) => { if (ContentFrame.Content is Page p) (p as ICleanup)?.Cleanup(); };
        Nav.SelectedItem = Nav.MenuItems[0];
    }

    public void ApplyTheme() =>
        Root.RequestedTheme = SettingsService.Instance.Theme switch { "light" => ElementTheme.Light, "dark" => ElementTheme.Dark, _ => ElementTheme.Default };

    public void ApplyPerformance() => AccentWash.Visibility = SettingsService.Instance.LowResource ? Visibility.Collapsed : Visibility.Visible;

    private static NavigationTransitionInfo Transition() =>
        SettingsService.Instance.LowResource ? new SuppressNavigationTransitionInfo() : new EntranceNavigationTransitionInfo();

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected) { Go(typeof(SettingsPage), null); return; }
        if (args.SelectedItemContainer?.Tag is not string tag) return;
        Navigate(tag);
    }

    private void Go(Type page, object? param)
    {
        if (ContentFrame.Content is Page old) (old as ICleanup)?.Cleanup();   // cancel running work before the page goes away
        ContentFrame.Navigate(page, param, Transition());
    }

    private void Navigate(string tag)
    {
        if (tag.StartsWith("adv:")) { Go(typeof(AdvancedPage), tag[4..]); return; }
        switch (tag)
        {
            case "overview": Go(typeof(OverviewPage), null); break;
            case "dns": Go(typeof(DnsPage), null); break;
            case "ping": Go(typeof(PingPage), null); break;
            case "traceroute": Go(typeof(TraceroutePage), null); break;
            case "networktest": Go(typeof(NetworkTestPage), null); break;
            case "speedtest": Go(typeof(SpeedTestPage), null); break;
            case "wifi": Go(typeof(WifiPage), null); break;
            case "ethernet": Go(typeof(AdaptersPage), "ethernet"); break;
            case "adapters": Go(typeof(AdaptersPage), "all"); break;
            case "diagnostics": Go(typeof(DiagnosticsPage), null); break;
            case "about": Go(typeof(AboutPage), null); break;
        }
    }

    /// <summary>Used by page buttons (quick actions etc.) to jump to another page and highlight it in the menu.</summary>
    public void NavigateTo(string tag, bool autoRun = false)
    {
        var item = Find(Nav.MenuItems, tag) ?? Find(Nav.FooterMenuItems, tag);
        if (item == null) return;
        if (autoRun) PendingAutoRun = tag;
        if (item.Parent is null) { }
        ExpandParents(item);
        Nav.SelectedItem = item;
    }
    public string? PendingAutoRun { get; set; }

    private static void ExpandParents(NavigationViewItem item)
    {
        DependencyObject? p = item;
        while (p != null) { if (p is NavigationViewItem n && n != item) n.IsExpanded = true; p = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(p); }
    }

    private static NavigationViewItem? Find(IList<object> items, string tag)
    {
        foreach (var o in items)
        {
            if (o is not NavigationViewItem i) continue;
            if (i.Tag as string == tag) return i;
            var c = Find(i.MenuItems, tag); if (c != null) return c;
        }
        return null;
    }
}

public interface ICleanup { void Cleanup(); }
