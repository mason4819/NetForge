using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Controls;
using NetForge.Helpers;
using NetForge.Models;
using NetForge.Services;

namespace NetForge.ViewModels;

public abstract class CancellableViewModel : ObservableObject
{
    protected CancellationTokenSource? Cts;
    private bool _running;
    public bool IsRunning { get => _running; protected set { Set(ref _running, value); Raise(nameof(IsIdle)); } }
    public bool IsIdle => !_running;
    public void Cancel() { try { Cts?.Cancel(); } catch { } }
}

public sealed class PingViewModel : CancellableViewModel
{
    private string _sent = "—", _recv = "—", _loss = "—", _min = "—", _avg = "—", _max = "—", _note = "", _err = "";
    public string Target { get; set; } = "1.1.1.1"; public double Packets { get; set; } = 4; public double TimeoutMs { get; set; } = 1000;
    public ObservableCollection<string> Lines { get; } = new();
    public string Sent { get => _sent; set => Set(ref _sent, value); } public string Received { get => _recv; set => Set(ref _recv, value); }
    public string Loss { get => _loss; set => Set(ref _loss, value); } public string Min { get => _min; set => Set(ref _min, value); }
    public string Avg { get => _avg; set => Set(ref _avg, value); } public string Max { get => _max; set => Set(ref _max, value); }
    public string Note { get => _note; set => Set(ref _note, value); } public string Error { get => _err; set => Set(ref _err, value); }

    public async Task StartAsync()
    {
        if (IsRunning || string.IsNullOrWhiteSpace(Target)) return;
        Cts = new CancellationTokenSource(); IsRunning = true; Lines.Clear(); Note = Loc.Get("Testing", "Testing…"); Error = "";
        var progress = new Progress<PingLine>(l => Lines.Add(l.Ms is long ms ? $"{l.Seq}.  {l.Address}  {ms} ms" : $"{l.Seq}.  {l.Address}  {l.Status}"));
        try
        {
            var s = await AppServices.Ping.PingAsync(Target, Math.Clamp((int)Packets, 1, 100), Math.Clamp((int)TimeoutMs, 100, 10000), progress, Cts.Token);
            Sent = s.Sent.ToString(); Received = s.Received.ToString(); Loss = $"{s.Loss:0}%";
            Min = s.Received > 0 ? $"{s.Min} ms" : "—"; Avg = s.Received > 0 ? $"{s.Avg:0} ms" : "—"; Max = s.Received > 0 ? $"{s.Max} ms" : "—";
            Note = s.Received == 0 ? Loc.Get("Ping_None", "No replies. The target may be down, or it ignores ping.")
                 : s.Loss == 0 && s.Avg < 100 ? Loc.Get("Ping_Good", "Yeah, no funny business this time.") : s.Loss > 0 ? Loc.Get("Ping_Loss", "Some packets got lost on the way.") : "";
        }
        catch (OperationCanceledException) { Note = Loc.Get("Cancelled", "Stopped."); }
        catch (Exception ex) { Note = ""; Error = Loc.Get("Ping_Err", "Couldn't reach or resolve that target.") + " " + ex.Message; }
        finally { IsRunning = false; Cts?.Dispose(); Cts = null; }
    }
}

public sealed class TraceViewModel : CancellableViewModel
{
    private string _status = "", _err = "";
    public string Target { get; set; } = "1.1.1.1";
    public ObservableCollection<TraceHop> Hops { get; } = new();
    public string Status { get => _status; set => Set(ref _status, value); } public string Error { get => _err; set => Set(ref _err, value); }

    public async Task StartAsync()
    {
        if (IsRunning || string.IsNullOrWhiteSpace(Target)) return;
        Cts = new CancellationTokenSource(); IsRunning = true; Hops.Clear(); Error = ""; Status = Loc.Get("Testing", "Testing…");
        try { await AppServices.Trace.TraceAsync(Target, 30, new Progress<TraceHop>(h => Hops.Add(h)), Cts.Token); Status = Loc.Get("Done", "Done."); }
        catch (OperationCanceledException) { Status = Loc.Get("Cancelled", "Stopped."); }
        catch (Exception ex) { Status = ""; Error = Loc.Get("Ping_Err", "Couldn't reach or resolve that target.") + " " + ex.Message; }
        finally { IsRunning = false; Cts?.Dispose(); Cts = null; }
    }
}

public sealed class NetworkTestViewModel : CancellableViewModel
{
    private string _summary = ""; private bool _hasFail;
    public ObservableCollection<TestItem> Items { get; } = new();
    public string Summary { get => _summary; set => Set(ref _summary, value); }
    public bool HasFailure { get => _hasFail; set => Set(ref _hasFail, value); }

    public async Task RunAsync()
    {
        if (IsRunning) return;
        Cts = new CancellationTokenSource(); IsRunning = true; Items.Clear(); HasFailure = false; Summary = Loc.Get("Testing", "Testing…");
        try
        {
            await AppServices.NetTest.RunAsync(i => Items.Add(i), Cts.Token);
            var fail = Items.FirstOrDefault(i => i.State == TestState.Failed);
            HasFailure = fail != null;
            Summary = fail != null
                ? Loc.Get("Test_Problem", "Problem:") + " " + fail.Name + "\n" + fail.Detail + (string.IsNullOrEmpty(fail.Hint) ? "" : "\n" + Loc.Get("Test_TryThis", "You can try:") + " " + fail.Hint)
                : Items.Any(i => i.State == TestState.Warning) ? Loc.Get("Test_DoneWarn", "Works, with a couple of notes above.") : "✓ " + Loc.Get("Msg_Connected_S", "Your network is alive and well.");
        }
        catch (OperationCanceledException) { Summary = Loc.Get("Cancelled", "Stopped."); }
        catch (Exception ex) { Logger.Write(ex); Summary = ex.Message; }
        finally { IsRunning = false; Cts?.Dispose(); Cts = null; }
    }
}

public sealed class SpeedViewModel : CancellableViewModel
{
    private string _stage = "", _ping = "—", _jit = "—", _loss = "—", _down = "—", _up = "—", _fail = "";
    public string BackendName => AppServices.Speed.BackendName;
    public string Stage { get => _stage; set => Set(ref _stage, value); } public string Ping { get => _ping; set => Set(ref _ping, value); }
    public string Jitter { get => _jit; set => Set(ref _jit, value); } public string Loss { get => _loss; set => Set(ref _loss, value); }
    public string Download { get => _down; set => Set(ref _down, value); } public string Upload { get => _up; set => Set(ref _up, value); }
    public string Failure { get => _fail; set => Set(ref _fail, value); }

    public async Task RunAsync()
    {
        if (IsRunning) return;
        Cts = new CancellationTokenSource(); IsRunning = true; Failure = ""; Ping = Jitter = Loss = Download = Upload = "—";
        try
        {
            var r = await AppServices.Speed.RunAsync(new Progress<string>(s => Stage = s), Cts.Token);
            Ping = r.PingMs is double p ? $"{p:0} ms" : "—"; Jitter = r.JitterMs is double j ? $"{j:0.#} ms" : "—"; Loss = r.LossPct is double l ? $"{l:0}%" : "—";
            Download = r.DownMbps is double d ? $"{d:0.#} Mbps" : "—"; Upload = r.UpMbps is double u ? $"{u:0.#} Mbps" : "—";
            if (r.DownMbps != null || r.UpMbps != null) { var s = SettingsService.Instance; s.LastDownMbps = r.DownMbps; s.LastUpMbps = r.UpMbps; s.Save(); }
            if (r.DownMbps == null || r.UpMbps == null) Failure = Loc.Get("Speed_Partial", "The test server didn't answer for part of the test, so those numbers are left blank rather than guessed.");
            Stage = Loc.Get("Done", "Done.");
        }
        catch (OperationCanceledException) { Stage = Loc.Get("Cancelled", "Stopped."); }
        finally { IsRunning = false; Cts?.Dispose(); Cts = null; }
    }
}

public sealed class WifiViewModel : ObservableObject
{
    private bool _busy, _unavail;
    public ObservableCollection<KeyValue> Items { get; } = new();
    public bool IsBusy { get => _busy; set => Set(ref _busy, value); }
    public bool Unavailable { get => _unavail; set => Set(ref _unavail, value); }
    public async Task LoadAsync()
    {
        IsBusy = true; Items.Clear();
        var rows = await AppServices.Wifi.GetAsync();
        Unavailable = rows == null; if (rows != null) foreach (var r in rows) Items.Add(r);
        IsBusy = false;
    }
}

public sealed class AdaptersViewModel : ObservableObject
{
    private readonly bool _ethernetOnly; private bool _busy, _empty;
    public AdaptersViewModel(bool ethernetOnly) => _ethernetOnly = ethernetOnly;
    public ObservableCollection<AdapterInfo> Items { get; } = new();
    public bool IsBusy { get => _busy; set => Set(ref _busy, value); }
    public bool IsEmpty { get => _empty; set => Set(ref _empty, value); }
    public async Task LoadAsync()
    {
        IsBusy = true; Items.Clear();
        var list = await Task.Run(() => AppServices.Adapters.GetAdapters());
        foreach (var a in list.Where(a => _ethernetOnly ? a.Kind == AdapterKind.Ethernet : true)) Items.Add(a);
        IsEmpty = Items.Count == 0; IsBusy = false;
    }
}

public sealed class DiagnosticsViewModel : CancellableViewModel
{
    private string _report = "";
    public string Report { get => _report; set { Set(ref _report, value); Raise(nameof(HasReport)); } }
    public bool HasReport => !string.IsNullOrEmpty(_report);
    public async Task BuildAsync()
    {
        if (IsRunning) return;
        Cts = new CancellationTokenSource(); IsRunning = true; Report = "";
        try { Report = await AppServices.Diagnostics.BuildReportAsync(Cts.Token); }
        catch (OperationCanceledException) { }
        finally { IsRunning = false; Cts?.Dispose(); Cts = null; }
    }
}

public sealed class SettingsViewModel : ObservableObject
{
    private static readonly string[] Themes = { "system", "light", "dark" };
    private static readonly string[] Langs = { "system", "en-US", "zh-TW", "zh-CN", "ja-JP", "ko-KR" };
    private readonly SettingsService _s = SettingsService.Instance;
    private bool _restart;
    public int ThemeIndex { get => Math.Max(0, Array.IndexOf(Themes, _s.Theme)); set { _s.Theme = Themes[value]; _s.Save(); App.MainWindowInstance.ApplyTheme(); Raise(); } }
    public int LanguageIndex { get => Math.Max(0, Array.IndexOf(Langs, _s.Language)); set { _s.Language = Langs[value]; _s.Save(); RestartNote = true; Raise(); } }
    public int PerformanceIndex { get => _s.LowResource ? 1 : 0; set { _s.LowResource = value == 1; _s.Save(); App.MainWindowInstance.ApplyPerformance(); Raise(); } }
    public bool RestartNote { get => _restart; set => Set(ref _restart, value); }
    public IList<string> ThemeOptions { get; } = new[] { Loc.Get("Theme_System", "Follow Windows"), Loc.Get("Theme_Light", "Light"), Loc.Get("Theme_Dark", "Dark") };
    public IList<string> LanguageOptions { get; } = new[] { Loc.Get("Lang_System", "Follow Windows"), "English", "繁體中文", "简体中文", "日本語", "한국어" };
}
