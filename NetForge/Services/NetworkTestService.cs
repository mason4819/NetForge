using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetForge.Helpers;
using NetForge.Models;

namespace NetForge.Services;

public sealed class NetworkTestService : INetworkTestService
{
    private readonly IAdapterService _adapters;
    public NetworkTestService(IAdapterService a) => _adapters = a;

    private static async Task<long?> PingOnceAsync(string host, int timeout = 1500, int payload = 32, bool dontFragment = false)
    {
        try
        {
            using var p = new Ping();
            var r = await p.SendPingAsync(IPAddress.Parse(host), timeout, new byte[payload], new PingOptions(128, dontFragment));
            return r.Status == IPStatus.Success ? r.RoundtripTime : null;
        }
        catch { return null; }
    }

    public async Task RunAsync(Action<TestItem> report, CancellationToken ct)
    {
        TestItem Add(string key, string en, TestState st, string detail, string hint = "")
        {
            var t = new TestItem { Name = Loc.Get(key, en), State = st, Detail = detail, Hint = hint };
            report(t); return t;
        }
        var na = Loc.Get("Unavailable", "Unavailable");
        var a = _adapters.GetPrimary();
        if (a == null)
        {
            Add("Test_Adapter", "Network adapter", TestState.Failed, Loc.Get("Test_NoAdapter", "No connected adapter with a gateway."), Loc.Get("Hint_Adapter", "Check the cable or Wi-Fi switch, then run the test again."));
            Add("Test_DefaultRoute", "Default route", TestState.Failed, na);
            return;
        }
        ct.ThrowIfCancellationRequested();
        Add("Test_Adapter", "Network adapter", TestState.Passed, $"{a.Name} ({a.KindText})");
        Add("Test_Dhcp", "DHCP", TestState.Passed, a.Dhcp == Loc.Get("On", "On") ? Loc.Get("Test_DhcpOn", "Automatic (DHCP)") : Loc.Get("Test_DhcpStatic", "Static address"));
        Add("Test_DefaultRoute", "Default route", a.HasGateway ? TestState.Passed : TestState.Failed, a.Gateway);

        var gw = a.Gateway.Split(',')[0].Trim();
        long? gwMs = IPAddress.TryParse(gw, out _) ? await PingOnceAsync(gw) : null;
        Add("Test_Gateway", "Gateway", gwMs != null ? TestState.Passed : TestState.Warning,
            gwMs != null ? $"{gw} · {gwMs} ms" : $"{gw} · " + Loc.Get("Test_GwSilent", "no reply (some routers ignore ping)"));
        ct.ThrowIfCancellationRequested();

        Add("Test_IPv4", "IPv4", a.IsApipa ? TestState.Failed : TestState.Passed, a.IPv4,
            a.IsApipa ? Loc.Get("Hint_Apipa", "A 169.254.x.x address means DHCP didn't answer. Try Release / Renew under Advanced > DHCP.") : "");
        Add("Test_IPv6", "IPv6", a.HasGlobalV6 ? TestState.Passed : TestState.Warning, a.HasGlobalV6 ? a.IPv6 : Loc.Get("Test_NoV6", "No global IPv6 address. Plenty of networks work fine without it."));
        Add("Test_Dns", "DNS", a.DnsConfigured ? TestState.Passed : TestState.Failed, a.Dns,
            a.DnsConfigured ? "" : Loc.Get("Hint_Dns", "No DNS server configured. Pick one in the DNS page."));

        // DNS resolution with a hard timeout
        TestState ds; string dd;
        try
        {
            var task = Dns.GetHostAddressesAsync("example.com", ct);
            if (await Task.WhenAny(task, Task.Delay(4000, ct)) == task && task.IsCompletedSuccessfully && task.Result.Length > 0) { ds = TestState.Passed; dd = "example.com → " + task.Result[0]; }
            else { ds = TestState.Failed; dd = Loc.Get("Test_DnsTimeout", "DNS server didn't answer in time."); }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { ds = TestState.Failed; dd = Loc.Get("Test_DnsFail", "Couldn't resolve example.com."); }
        Add("Test_DnsResolve", "DNS resolution", ds, dd, ds == TestState.Failed ? Loc.Get("Hint_DnsResolve", "DNS server isn't answering. Switching to another DNS often fixes it.") : "");
        ct.ThrowIfCancellationRequested();

        long? net = await PingOnceAsync("1.1.1.1") ?? await PingOnceAsync("8.8.8.8");
        if (net == null)
        {
            try { using var c = new TcpClient(); using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(2500); await c.ConnectAsync("1.1.1.1", 443, cts.Token); net = 0; }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { }
        }
        Add("Test_Internet", "Internet", net != null ? TestState.Passed : TestState.Failed, net != null ? Loc.Get("Test_Reachable", "Reachable") : Loc.Get("Test_Unreachable", "Can't reach the internet."),
            net != null ? "" : Loc.Get("Hint_Internet", "The router is there but the internet isn't. Check the modem or call your ISP."));

        int ok = 0; const int n = 10;
        for (int i = 0; i < n; i++) { ct.ThrowIfCancellationRequested(); if (await PingOnceAsync("1.1.1.1", 1000) != null) ok++; await Task.Delay(150, ct); }
        double loss = 100.0 * (n - ok) / n;
        Add("Test_Loss", "Packet loss", loss == 0 ? TestState.Passed : loss <= 20 ? TestState.Warning : TestState.Failed, $"{loss:0}%  ({ok}/{n})");

        var df = await PingOnceAsync("1.1.1.1", 1500, 1472, true);
        Add("Test_Mtu", "MTU", df != null ? TestState.Passed : TestState.Warning, $"{a.Mtu}" + (df != null ? "" : " · " + Loc.Get("Test_MtuSmall", "1500-byte packets didn't make it through. Path MTU may be lower.")));
    }
}
