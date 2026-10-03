using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using NetForge.Helpers;

namespace NetForge.Services;

/// <summary>Builds a plain-text report. Deliberately leaves out passwords, Wi-Fi keys, BSSID, user files and account info.</summary>
public sealed class DiagnosticsService : IDiagnosticsService
{
    private readonly IAdapterService _adapters; private readonly IWifiService _wifi;
    public DiagnosticsService(IAdapterService a, IWifiService w) { _adapters = a; _wifi = w; }

    public async Task<string> BuildReportAsync(CancellationToken ct)
    {
        var sb = new StringBuilder(); var errors = new List<string>();
        void H(string t) { sb.AppendLine(); sb.AppendLine("=== " + t + " ==="); }
        sb.AppendLine("NetForge Network Diagnostic"); sb.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
        H("Windows Version"); sb.AppendLine(RuntimeInformation.OSDescription + " (" + RuntimeInformation.OSArchitecture + ")");

        H("Network Adapters");
        var list = _adapters.GetAdapters();
        foreach (var a in list) sb.AppendLine($"{a.Name} | {a.KindText} | {a.StatusText} | {a.Speed} | MAC {a.Mac} | MTU {a.Mtu}");
        H("IP Configuration (IPv4 / IPv6 / Gateway / DNS)");
        foreach (var a in list.Where(x => x.IsUp && x.Kind != Models.AdapterKind.Loopback))
            sb.AppendLine($"[{a.Name}] IPv4 {a.IPv4} | IPv6 {a.IPv6} | Gateway {a.Gateway} | DNS {a.Dns} | DHCP {a.Dhcp}");

        H("Routing"); try { sb.AppendLine(await Sys.ReadAsync("route print", ct)); } catch (Exception ex) { errors.Add("Routing: " + ex.Message); }
        H("Wi-Fi");
        try { var w = await _wifi.GetAsync(); if (w == null) sb.AppendLine("Not connected / unavailable"); else foreach (var kv in w.Where(k => k.Label != "BSSID")) sb.AppendLine($"{kv.Label}: {kv.Value}"); }
        catch (Exception ex) { errors.Add("Wi-Fi: " + ex.Message); }

        H("Ping");
        var primary = _adapters.GetPrimary();
        var targets = new List<string>(); if (primary != null && IPAddress.TryParse(primary.Gateway.Split(',')[0].Trim(), out _)) targets.Add(primary.Gateway.Split(',')[0].Trim());
        targets.Add("1.1.1.1"); targets.Add("8.8.8.8");
        foreach (var t in targets)
        {
            try { var s = await AppServices.Ping.PingAsync(t, 4, 1000, new Progress<PingLine>(), ct); sb.AppendLine($"{t}: sent {s.Sent}, received {s.Received}, loss {s.Loss:0}%, min/avg/max {s.Min}/{s.Avg:0}/{s.Max} ms"); }
            catch (OperationCanceledException) { throw; } catch (Exception ex) { errors.Add($"Ping {t}: {ex.Message}"); }
        }
        H("DNS Resolution");
        foreach (var host in new[] { "example.com", "www.microsoft.com" })
        {
            try { var sw = System.Diagnostics.Stopwatch.StartNew(); var r = await Dns.GetHostAddressesAsync(host, ct); sb.AppendLine($"{host}: {string.Join(", ", r.Take(3))} ({sw.ElapsedMilliseconds} ms)"); }
            catch (OperationCanceledException) { throw; } catch (Exception ex) { sb.AppendLine($"{host}: FAILED"); errors.Add($"DNS {host}: {ex.Message}"); }
        }
        H("Connectivity"); sb.AppendLine("Network available: " + NetworkInterface.GetIsNetworkAvailable());
        sb.AppendLine("Primary adapter: " + (primary?.Name ?? "none"));
        H("Errors"); if (errors.Count == 0) sb.AppendLine("None"); else errors.ForEach(e => sb.AppendLine(e));
        return sb.ToString();
    }
}
