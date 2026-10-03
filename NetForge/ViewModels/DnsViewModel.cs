using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Controls;
using NetForge.Helpers;
using NetForge.Models;
using NetForge.Services;

namespace NetForge.ViewModels;

public sealed class DnsViewModel : ObservableObject
{
    private static readonly string[] CatKeys = { "General", "Privacy", "Security", "Family", "Custom" };
    private int _cat; private DnsProvider? _sel; private bool _busy, _hasStatus, _needsAdmin; private string _statusT = "", _statusM = "", _reason = "", _details = "";
    private InfoBarSeverity _sev;

    public IList<string> Categories { get; } = CatKeys.Select(k => Loc.Get("Cat_" + k, k)).ToList();
    public IList<string> Scopes { get; } = new[] { Loc.Get("Scope_Wifi", "Wi-Fi"), Loc.Get("Scope_Ethernet", "Ethernet"), Loc.Get("Scope_All", "All adapters") };
    public IList<string> Families { get; } = new[] { "IPv4", "IPv6", "IPv4 + IPv6" };
    public ObservableCollection<DnsProvider> Filtered { get; } = new();
    public int ScopeIndex { get; set; } = 0; public int FamilyIndex { get; set; } = 0;
    public string CustomP4 { get; set; } = ""; public string CustomS4 { get; set; } = ""; public string CustomP6 { get; set; } = ""; public string CustomS6 { get; set; } = "";
    public bool IsCustom => _cat == 4;
    public bool IsNotCustom => _cat != 4;
    public bool IsIdle => !_busy;
    public int CategoryIndex { get => _cat; set { if (Set(ref _cat, value)) { Raise(nameof(IsCustom)); Raise(nameof(IsNotCustom)); Refilter(); } } }
    public DnsProvider? Selected { get => _sel; set { if (Set(ref _sel, value)) UpdateDetails(); } }
    public string Reason { get => _reason; set { Set(ref _reason, value); Raise(nameof(HasReason)); } }
    public bool HasReason => !string.IsNullOrEmpty(_reason);
    public string Details { get => _details; set => Set(ref _details, value); }
    public bool IsBusy { get => _busy; set { Set(ref _busy, value); Raise(nameof(IsIdle)); } }
    public bool HasStatus { get => _hasStatus; set => Set(ref _hasStatus, value); }
    public bool NeedsAdmin { get => _needsAdmin; set => Set(ref _needsAdmin, value); }
    public string StatusTitle { get => _statusT; set => Set(ref _statusT, value); }
    public string StatusMessage { get => _statusM; set => Set(ref _statusM, value); }
    public InfoBarSeverity Severity { get => _sev; set => Set(ref _sev, value); }

    public DnsViewModel() { Refilter(); }

    private void Refilter()
    {
        Filtered.Clear();
        if (_cat < 4) foreach (var p in AppServices.Dns.Providers.Where(p => p.Categories.Contains(CatKeys[_cat]))) Filtered.Add(p);
        Selected = Filtered.FirstOrDefault();
        UpdateDetails();
    }

    public void ChooseByPurpose(int purpose)
    {
        Reason = Loc.Get("Reason_" + CatKeys[purpose], "");
        CategoryIndex = purpose;
    }

    private void UpdateDetails()
    {
        if (IsCustom || Selected == null) { Details = ""; return; }
        Details = $"{Selected.Description}\n{Loc.Get("Dns_UseFor", "Good for")}: {Selected.Usage}\n\n{Selected.V4Line}\n{Selected.V6Line}";
    }

    private void Show(bool ok, string title, string message, bool admin = false)
    { HasStatus = true; Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Error; StatusTitle = title; StatusMessage = message; NeedsAdmin = admin; }

    private (string[] v4, string[] v6, string name)? Target()
    {
        if (IsCustom) return (new[] { CustomP4, CustomS4 }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToArray(),
                              new[] { CustomP6, CustomS6 }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToArray(), Loc.Get("Cat_Custom", "Custom"));
        return Selected == null ? null : (Selected.V4, Selected.V6, Selected.Name);
    }

    public async Task ApplyAsync()
    {
        var t = Target(); if (t == null) return;
        if (IsCustom)
        {
            foreach (var s in t.Value.v4.Concat(t.Value.v6)) if (!System.Net.IPAddress.TryParse(s, out _)) { Show(false, Loc.Get("Dns_Failed_T", "DNS wasn't applied"), Loc.F("Dns_BadIp", "'{0}' isn't a valid IP address.", s)); return; }
        }
        IsBusy = true; HasStatus = false;
        var r = await AppServices.Dns.ApplyAsync(t.Value.v4, t.Value.v6, (DnsScope)ScopeIndex, (IpFamily)FamilyIndex);
        IsBusy = false;
        if (r.Ok) Show(true, "✓ " + Loc.Get("Dns_Applied_T", "DNS switched"), Loc.F("Dns_Applied_S", "{0} DNS is on.\nNetwork: \"Got it.\"", t.Value.name));
        else Show(false, "✕ " + Loc.Get("Dns_Failed_T", "DNS wasn't applied"),
            r.NeedsAdmin ? Loc.Get("Dns_NeedAdmin", "Windows wouldn't let us change it. This needs administrator permission.") : Loc.Get("Dns_FailWhy", "Reason:") + " " + r.Message, r.NeedsAdmin);
    }

    public async Task BackupAsync()
    { var r = await AppServices.Dns.BackupAsync(); Show(r.Ok, r.Ok ? "✓ " + Loc.Get("Dns_BackupDone", "Backup saved") : "✕ " + Loc.Get("Dns_Failed_T", "DNS wasn't applied"), r.Ok ? Loc.Get("Dns_BackupDone_S", "Your current DNS settings are saved.") : r.Message); }

    public async Task RestoreAsync()
    {
        IsBusy = true; var r = await AppServices.Dns.RestoreAsync(); IsBusy = false;
        Show(r.Ok, r.Ok ? "✓ " + Loc.Get("Dns_RestoreDone", "DNS restored") : "✕ " + Loc.Get("Dns_RestoreFail", "Couldn't restore DNS"), r.Ok ? Loc.Get("Dns_RestoreDone_S", "Your previous DNS settings are back.") : r.Message, r.NeedsAdmin);
    }
}
