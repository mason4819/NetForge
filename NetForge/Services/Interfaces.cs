using NetForge.Models;

namespace NetForge.Services;

public interface IAdapterService { IReadOnlyList<AdapterInfo> GetAdapters(); AdapterInfo? GetPrimary(); }
public interface IDnsService
{
    IReadOnlyList<DnsProvider> Providers { get; }
    IReadOnlyList<AdapterInfo> ResolveTargets(DnsScope scope);
    bool HasBackup { get; }
    Task<OpResult> BackupAsync();
    Task<OpResult> ApplyAsync(string[] v4, string[] v6, DnsScope scope, IpFamily family);
    Task<OpResult> RestoreAsync();
}
public record PingLine(int Seq, string Address, long? Ms, string Status);
public sealed class PingSummary { public int Sent, Received; public long Min, Max; public double Avg; public double Loss => Sent == 0 ? 0 : 100.0 * (Sent - Received) / Sent; }
public interface IPingService { Task<PingSummary> PingAsync(string target, int count, int timeoutMs, IProgress<PingLine> progress, CancellationToken ct); }
public interface ITracerouteService { Task TraceAsync(string target, int maxHops, IProgress<TraceHop> progress, CancellationToken ct); }
public interface INetworkTestService { Task RunAsync(Action<TestItem> report, CancellationToken ct); }
public sealed class SpeedResult { public double? PingMs, JitterMs, LossPct, DownMbps, UpMbps; }
public interface ISpeedTestService { string BackendName { get; } Task<SpeedResult> RunAsync(IProgress<string> stage, CancellationToken ct); }
public interface IWifiService { Task<List<KeyValue>?> GetAsync(); }
public interface IDiagnosticsService { Task<string> BuildReportAsync(CancellationToken ct); }

public static class AppServices
{
    public static IAdapterService Adapters { get; } = new AdapterService();
    public static IDnsService Dns { get; } = new DnsService(Adapters);
    public static IPingService Ping { get; } = new PingService();
    public static ITracerouteService Trace { get; } = new TracerouteService();
    public static INetworkTestService NetTest { get; } = new NetworkTestService(Adapters);
    public static ISpeedTestService Speed { get; set; } = new CloudflareSpeedTestService();   // pluggable: assign another ISpeedTestService here
    public static IWifiService Wifi { get; } = new WifiService();
    public static IDiagnosticsService Diagnostics { get; } = new DiagnosticsService(Adapters, Wifi);
}
