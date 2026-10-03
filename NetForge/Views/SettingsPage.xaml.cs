using Microsoft.UI.Xaml.Controls;
using NetForge.ViewModels;

namespace NetForge.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel Vm { get; } = new();
    public SettingsPage() { InitializeComponent(); }
}
