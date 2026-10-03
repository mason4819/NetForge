using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NetForge.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace NetForge.Views;

public sealed partial class DiagnosticsPage : Page, ICleanup
{
    public DiagnosticsViewModel Vm { get; } = new();
    public DiagnosticsPage() { InitializeComponent(); }
    private async void Build_Click(object s, RoutedEventArgs e) => await Vm.BuildAsync();
    public void Cleanup() => Vm.Cancel();

    private async void Save_Click(object s, RoutedEventArgs e)
    {
        var picker = new FileSavePicker { SuggestedStartLocation = PickerLocationId.Desktop, SuggestedFileName = "NetForge-Network-Diagnostic" };
        picker.FileTypeChoices.Add("Text", new List<string> { ".txt" });
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindowInstance));
        var file = await picker.PickSaveFileAsync();
        if (file != null) await FileIO.WriteTextAsync(file, Vm.Report);
    }

    private void Copy_Click(object s, RoutedEventArgs e)
    {
        var dp = new DataPackage(); dp.SetText(Vm.Report); Clipboard.SetContent(dp);
    }
}
