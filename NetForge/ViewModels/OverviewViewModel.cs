using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using NetForge.Helpers;
using NetForge.Models;
using NetForge.Services;

namespace NetForge.ViewModels;

public sealed class OverviewViewModel : ObservableObject
{
    private string _glyph = "…", _title = "", _sub = ""; private Brush? _brush; private bool _busy;
    public string Glyph { get => _glyph; set => Set(ref _glyph, value); }
    public string Title { get => _title; set => Set(ref _title, value); }
    public string Sub { get => _sub; set => Set(ref _sub, value); }
    public Brush? StatusBrush { get => _brush; set => Set(ref _brush, value); }
    public bool IsBusy { get => _busy; set => Set(ref _busy, value); }
    public ObservableCollection<KeyValue> Items { get; } = new();

    private static Brush Res(string key) => (Brush)Application.Current.Resources[key];

    public async Task LoadAsync()
    {
        IsBusy = true; Title = Loc.Get("Checking", "Checking…"); Sub = ""; Glyph = "…";
        var primary = await Task.Run(() => AppServices.Adapters.GetPrimary());
        var na = Loc.Get("Unavailable", "Unavailable");
        Items.Clear();
        Items.Add(new("IPv4", primary?.IPv4 ?? na)); Items.Add(new("IPv6", primary?.IPv6 ?? na));
        Items.Add(new(Loc.Get("Lbl_Gateway", "Gateway"), primary?.Gateway ?? na)); Items.Add(new("DNS", primary?.Dns ?? na));
        Items.Add(new(Loc.Get("Lbl_Adapter", "Adapter"), primary?.Name ?? na));
        var s = SettingsService.Instance; var none = Loc.Get("Overview_NoSpeed", "Run a speed test");
        Items.Add(new("Download", s.LastDownMbps is double d ? $"{d:0.#} Mbps" : none)); Items.Add(new("Upload", s.LastUpMbps is double u ? $"{u:0.#} Mbps" : none));

        if (primary == null)
        {
            Glyph = "✕"; StatusBrush = Res("SystemFillColorCriticalBrush");
            Title = Loc.Get("Msg_Disconnected_T", "No connection"); Sub = Loc.Get("Msg_Disconnected_S", "Looks like the network ran off. Let's start with the simplest checks.");
        }
        else
        {
            bool net = false;
            try { using var p = new Ping(); net = (await p.SendPingAsync("1.1.1.1", 1500)).Status == IPStatus.Success || (await p.SendPingAsync("8.8.8.8", 1500)).Status == IPStatus.Success; } catch { }
            if (net) { Glyph = "●"; StatusBrush = Res("SystemFillColorSuccessBrush"); Title = Loc.Get("Msg_Connected_T", "Connected"); Sub = Loc.Get("Msg_Connected_S", "Your network is alive and well."); }
            else { Glyph = "⚠"; StatusBrush = Res("SystemFillColorCautionBrush"); Title = Loc.Get("Msg_Limited_T", "Limited"); Sub = Loc.Get("Msg_Limited_S", "You're connected to the router, but the internet isn't answering."); }
        }
        IsBusy = false;
    }
}
