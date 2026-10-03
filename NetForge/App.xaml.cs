using System.Text;
using Microsoft.UI.Xaml;
using NetForge.Services;

namespace NetForge;

public partial class App : Application
{
    public static MainWindow MainWindowInstance { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var s = SettingsService.Instance;
        s.Load();
        if (s.Language != "system")
            Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = s.Language;
        UnhandledException += (_, e) => { Logger.Write(e.Exception); e.Handled = true; };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindowInstance = new MainWindow();
        MainWindowInstance.Activate();
    }
}
