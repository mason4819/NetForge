using NetForge.Helpers;
using NetForge.Services;

namespace NetForge.ViewModels;

public sealed class AdvancedViewModel : ObservableObject
{
    private string _raw = "", _filter = "", _resultT = "", _resultM = ""; private bool _busy, _hasResult, _resultOk, _empty;
    public AdvSection Section { get; }
    public string Title { get; }
    public string Tip => Section.Tip;
    public bool HasView => Section.ReadScript != null;
    public bool HasTip => !string.IsNullOrEmpty(Section.Tip);
    public IReadOnlyList<AdvAction> Actions => Section.Actions;
    public bool HasActions => Section.Actions.Length > 0;
    public bool IsAdmin => Elevation.IsAdmin;

    public AdvancedViewModel(string key) { Section = AdvancedCatalog.Get(key); Title = Loc.Get("Nav_" + key + "/Content", key); }

    public string Filter { get => _filter; set { if (Set(ref _filter, value)) { Raise(nameof(Output)); } } }
    public string Output
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_filter)) return _raw;
            return string.Join("\n", _raw.Split('\n').Where(l => l.Contains(_filter, StringComparison.OrdinalIgnoreCase)).Select(l => l.TrimEnd('\r')));
        }
    }
    public bool IsEmpty { get => _empty; set => Set(ref _empty, value); }
    public bool IsBusy { get => _busy; set => Set(ref _busy, value); }
    public bool HasResult { get => _hasResult; set => Set(ref _hasResult, value); }
    public bool ResultOk { get => _resultOk; set => Set(ref _resultOk, value); }
    public string ResultTitle { get => _resultT; set => Set(ref _resultT, value); }
    public string ResultMessage { get => _resultM; set => Set(ref _resultM, value); }

    public async Task LoadAsync()
    {
        if (Section.ReadScript == null) return;
        IsBusy = true;
        try { _raw = await Sys.ReadAsync(Section.ReadScript); }
        catch (Exception ex) { Logger.Write(ex); _raw = ex.Message; }
        IsEmpty = string.IsNullOrWhiteSpace(_raw);
        Raise(nameof(Output)); IsBusy = false;
    }

    public async Task RunAsync(AdvAction a, IDictionary<string, string> values)
    {
        IsBusy = true; HasResult = false;
        var r = await Sys.RunAsync(a.Build(values, true), a.Admin);
        IsBusy = false; HasResult = true; ResultOk = r.Ok;
        ResultTitle = r.Ok ? "✓ " + Loc.Get("Result_Done", "Done") : "✕ " + Loc.Get("Result_Failed", "That didn't work");
        ResultMessage = r.Ok ? (string.IsNullOrWhiteSpace(r.Message) || r.Message == "OK" ? a.Label : r.Message)
            : (r.NeedsAdmin ? r.Message : Loc.Get("Result_Why", "What happened:") + " " + r.Message + (a.Admin && !Elevation.IsAdmin ? "\n" + Loc.Get("Result_AdminHint", "This needs administrator permission.") : ""));
        if (r.Ok) await LoadAsync();
    }
}
