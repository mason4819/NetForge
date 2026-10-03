using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using NetForge.Helpers;
using NetForge.Models;

namespace NetForge.Services;

public sealed class DnsService : IDnsService
{
    private readonly IAdapterService _adapters;
    public DnsService(IAdapterService a) => _adapters = a;
    public IReadOnlyList<DnsProvider> Providers => DnsProvider.All;
    private static string BackupPath => Path.Combine(SettingsService.DataDir, "dns-backup.json");
    public bool HasBackup => File.Exists(BackupPath);

    public IReadOnlyList<AdapterInfo> ResolveTargets(DnsScope scope) =>
        _adapters.GetAdapters().Where(a => a.IsUp && scope switch
        {
            DnsScope.Wifi => a.Kind == AdapterKind.Wifi,
            DnsScope.Ethernet => a.Kind == AdapterKind.Ethernet,
            _ => a.Kind is AdapterKind.Wifi or AdapterKind.Ethernet
        }).ToList();

    private sealed class Snapshot
    {
        public string Name { get; set; } = ""; public int Index4 { get; set; } public int Index6 { get; set; }
        public bool Static4 { get; set; } public string[] V4 { get; set; } = Array.Empty<string>();
        public bool Static6 { get; set; } public string[] V6 { get; set; } = Array.Empty<string>();
    }

    // Reads the registry (readable without admin) to tell "set by hand" apart from "handed out by DHCP".
    private static string[] StaticServers(string guid, bool v6)
    {
        try
        {
            var path = (v6 ? @"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters\Interfaces\" : @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\") + guid;
            using var k = Registry.LocalMachine.OpenSubKey(path);
            var v = k?.GetValue("NameServer") as string;
            return string.IsNullOrWhiteSpace(v) ? Array.Empty<string>() : v.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }
        catch { return Array.Empty<string>(); }
    }

    public Task<OpResult> BackupAsync()
    {
        try
        {
            var snaps = _adapters.GetAdapters().Where(a => a.Kind is AdapterKind.Wifi or AdapterKind.Ethernet or AdapterKind.Other or AdapterKind.Vpn && a.Index4 != 0).Select(a =>
            {
                var s4 = StaticServers(a.Id, false); var s6 = StaticServers(a.Id, true);
                return new Snapshot { Name = a.Name, Index4 = a.Index4, Index6 = a.Index6, Static4 = s4.Length > 0, V4 = s4, Static6 = s6.Length > 0, V6 = s6 };
            }).ToList();
            File.WriteAllText(BackupPath, JsonSerializer.Serialize(snaps, new JsonSerializerOptions { WriteIndented = true }));
            return Task.FromResult(new OpResult(true, ""));
        }
        catch (Exception ex) { Logger.Write(ex); return Task.FromResult(new OpResult(false, ex.Message)); }
    }

    private static void AppendSet(StringBuilder sb, string fam, int index, string[] servers)
    {
        sb.AppendLine($"Nf 'interface {fam} set dnsservers name={index} source=static address={servers[0]} validate=no'");
        for (int i = 1; i < servers.Length; i++)
            sb.AppendLine($"Nf 'interface {fam} add dnsservers name={index} address={servers[i]} index={i + 1} validate=no'");
    }

    private const string Header = "function Nf($a){ $o = & netsh ($a -split ' ') 2>&1; if($LASTEXITCODE -ne 0){ throw ('netsh ' + $a + ' -> ' + ($o | Out-String)) } }\n";

    public async Task<OpResult> ApplyAsync(string[] v4, string[] v6, DnsScope scope, IpFamily family)
    {
        var targets = ResolveTargets(scope);
        if (targets.Count == 0) return new OpResult(false, Loc.Get("Dns_NoAdapter", "No connected adapter matches that choice."));
        var use4 = family != IpFamily.V6 ? v4.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray() : Array.Empty<string>();
        var use6 = family != IpFamily.V4 ? v6.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray() : Array.Empty<string>();
        if (use4.Length == 0 && use6.Length == 0) return new OpResult(false, Loc.Get("Dns_NoAddress", "This provider has no address for that IP type."));
        await BackupAsync();   // always back up before touching anything
        var sb = new StringBuilder(Header);
        foreach (var t in targets)
        {
            if (use4.Length > 0 && t.Index4 != 0) AppendSet(sb, "ipv4", t.Index4, use4);
            if (use6.Length > 0 && t.Index6 != 0) AppendSet(sb, "ipv6", t.Index6, use6);
        }
        sb.AppendLine("Clear-DnsClientCache -ErrorAction SilentlyContinue");
        return await Sys.RunAsync(sb.ToString(), admin: true);
    }

    public async Task<OpResult> RestoreAsync()
    {
        if (!HasBackup) return new OpResult(false, Loc.Get("Dns_NoBackup", "There's no backup yet."));
        List<Snapshot>? snaps;
        try { snaps = JsonSerializer.Deserialize<List<Snapshot>>(File.ReadAllText(BackupPath)); } catch (Exception ex) { return new OpResult(false, ex.Message); }
        if (snaps == null || snaps.Count == 0) return new OpResult(false, Loc.Get("Dns_NoBackup", "There's no backup yet."));
        var live = _adapters.GetAdapters().ToDictionary(a => a.Id + "|" + a.Name, a => a);
        var sb = new StringBuilder(Header);
        foreach (var s in snaps)
        {
            if (s.Index4 != 0) { if (s.Static4 && s.V4.Length > 0) AppendSet(sb, "ipv4", s.Index4, s.V4); else sb.AppendLine($"Nf 'interface ipv4 set dnsservers name={s.Index4} source=dhcp'"); }
            if (s.Index6 != 0) { if (s.Static6 && s.V6.Length > 0) AppendSet(sb, "ipv6", s.Index6, s.V6); else sb.AppendLine($"Nf 'interface ipv6 set dnsservers name={s.Index6} source=dhcp'"); }
        }
        sb.AppendLine("Clear-DnsClientCache -ErrorAction SilentlyContinue");
        return await Sys.RunAsync(sb.ToString(), admin: true);
    }
}
