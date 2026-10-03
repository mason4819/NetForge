using System.Diagnostics;
using System.Net.NetworkInformation;
using NetForge.Helpers;

namespace NetForge.Services;

/// <summary>
/// Default speed-test backend: Cloudflare's public speed endpoints (the same ones speed.cloudflare.com uses).
/// It is a plain, swappable ISpeedTestService; assign another implementation to AppServices.Speed to use a different server.
/// Nothing here is simulated: if a request fails, that number stays empty.
/// </summary>
public sealed class CloudflareSpeedTestService : ISpeedTestService
{
    public string BackendName => "Cloudflare (speed.cloudflare.com)";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<SpeedResult> RunAsync(IProgress<string> stage, CancellationToken ct)
    {
        var r = new SpeedResult();
        stage.Report(Loc.Get("Speed_StagePing", "Testing latency…"));
        var times = new List<long>(); int sent = 0;
        using (var p = new Ping())
            for (int i = 0; i < 10; i++)
            {
                ct.ThrowIfCancellationRequested(); sent++;
                try { var pr = await p.SendPingAsync("1.1.1.1", 1000); if (pr.Status == IPStatus.Success) times.Add(pr.RoundtripTime); } catch { }
                await Task.Delay(100, ct);
            }
        if (times.Count > 0)
        {
            r.PingMs = times.Average();
            r.JitterMs = times.Count > 1 ? times.Zip(times.Skip(1), (a, b) => Math.Abs(a - b)).Average() : 0;
        }
        r.LossPct = 100.0 * (sent - times.Count) / sent;

        stage.Report(Loc.Get("Speed_StageDown", "Testing download…"));
        try
        {
            long bytes = 0; var sw = Stopwatch.StartNew(); var buf = new byte[81920];
            while (sw.Elapsed < TimeSpan.FromSeconds(8))
            {
                ct.ThrowIfCancellationRequested();
                using var resp = await Http.GetAsync("https://speed.cloudflare.com/__down?bytes=26214400", HttpCompletionOption.ResponseHeadersRead, ct);
                resp.EnsureSuccessStatusCode();
                await using var s = await resp.Content.ReadAsStreamAsync(ct);
                int n;
                while ((n = await s.ReadAsync(buf, ct)) > 0) { bytes += n; if (sw.Elapsed > TimeSpan.FromSeconds(8)) break; }
            }
            if (bytes > 0) r.DownMbps = bytes * 8.0 / sw.Elapsed.TotalSeconds / 1e6;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { Logger.Write(ex); }

        stage.Report(Loc.Get("Speed_StageUp", "Testing upload…"));
        try
        {
            var payload = new byte[4 * 1024 * 1024]; Random.Shared.NextBytes(payload);
            long bytes = 0; var sw = Stopwatch.StartNew();
            while (sw.Elapsed < TimeSpan.FromSeconds(8))
            {
                ct.ThrowIfCancellationRequested();
                using var resp = await Http.PostAsync("https://speed.cloudflare.com/__up", new ByteArrayContent(payload), ct);
                resp.EnsureSuccessStatusCode();
                bytes += payload.Length;
            }
            if (bytes > 0) r.UpMbps = bytes * 8.0 / sw.Elapsed.TotalSeconds / 1e6;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { Logger.Write(ex); }
        return r;
    }
}
