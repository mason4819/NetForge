using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace NetForge.Services;

public sealed class PingService : IPingService
{
    public static async Task<IPAddress> ResolveAsync(string target, CancellationToken ct)
    {
        if (IPAddress.TryParse(target, out var ip)) return ip;
        var all = await Dns.GetHostAddressesAsync(target, ct);
        return all.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? all.First();
    }

    public async Task<PingSummary> PingAsync(string target, int count, int timeoutMs, IProgress<PingLine> progress, CancellationToken ct)
    {
        var addr = await ResolveAsync(target.Trim(), ct);
        var sum = new PingSummary(); long total = 0; sum.Min = long.MaxValue;
        using var p = new Ping();
        var buf = new byte[32];
        for (int i = 1; i <= count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            PingReply? r = null;
            try { r = await p.SendPingAsync(addr, timeoutMs, buf, new PingOptions(128, true)); } catch (PingException) { }
            sum.Sent++;
            if (r?.Status == IPStatus.Success)
            {
                sum.Received++; total += r.RoundtripTime; sum.Min = Math.Min(sum.Min, r.RoundtripTime); sum.Max = Math.Max(sum.Max, r.RoundtripTime);
                progress.Report(new PingLine(i, addr.ToString(), r.RoundtripTime, "OK"));
            }
            else progress.Report(new PingLine(i, addr.ToString(), null, r?.Status.ToString() ?? "Error"));
            if (i < count) { var wait = 1000 - (int)sw.ElapsedMilliseconds; if (wait > 0) await Task.Delay(wait, ct); }
        }
        if (sum.Received == 0) sum.Min = 0; else sum.Avg = (double)total / sum.Received;
        return sum;
    }
}

public sealed class TracerouteService : ITracerouteService
{
    public async Task TraceAsync(string target, int maxHops, IProgress<Models.TraceHop> progress, CancellationToken ct)
    {
        var dest = await PingService.ResolveAsync(target.Trim(), ct);
        using var p = new Ping();
        var buf = new byte[32];
        for (int ttl = 1; ttl <= maxHops; ttl++)
        {
            ct.ThrowIfCancellationRequested();
            var hop = new Models.TraceHop { Hop = ttl, Status = "Timeout" };
            PingReply? reply = null;
            for (int attempt = 0; attempt < 2 && reply?.Status is null or IPStatus.TimedOut; attempt++)
            {
                try { reply = await p.SendPingAsync(dest, 1500, buf, new PingOptions(ttl, true)); } catch (PingException) { reply = null; }
            }
            bool done = false;
            if (reply != null && (reply.Status == IPStatus.TtlExpired || reply.Status == IPStatus.TimeExceeded || reply.Status == IPStatus.Success))
            {
                hop.Address = reply.Address?.ToString().Split('%')[0] ?? "*";
                hop.Latency = reply.RoundtripTime <= 0 ? "<1 ms" : reply.RoundtripTime + " ms";
                hop.Status = reply.Status == IPStatus.Success ? "Reached" : "OK";
                done = reply.Status == IPStatus.Success;
                if (reply.Address != null)
                {
                    var lookup = Dns.GetHostEntryAsync(reply.Address);
                    if (await Task.WhenAny(lookup, Task.Delay(1200, ct)) == lookup && lookup.IsCompletedSuccessfully) hop.Hostname = lookup.Result.HostName;
                    else _ = lookup.ContinueWith(t => { var _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                }
            }
            progress.Report(hop);
            if (done) break;
        }
    }
}
