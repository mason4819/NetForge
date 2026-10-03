using NetForge.Helpers;

namespace NetForge.Models;

public record KeyValue(string Label, string Value);
public record OpResult(bool Ok, string Message, bool NeedsAdmin = false);
public enum DnsScope { Wifi, Ethernet, All }
public enum IpFamily { V4, V6, Both }
public enum AdapterKind { Wifi, Ethernet, Bluetooth, Vpn, Virtual, Loopback, Other }
public enum TestState { Passed, Warning, Failed }

public sealed class DnsProvider
{
    public DnsProvider(string id, string name, string[] cats, string[] v4, string[] v6) { Id = id; Name = name; Categories = cats; V4 = v4; V6 = v6; }
    public string Id { get; } public string Name { get; }
    public string[] Categories { get; } public string[] V4 { get; } public string[] V6 { get; }
    public string Primary4 => V4.ElementAtOrDefault(0) ?? "—";
    public string Secondary4 => V4.ElementAtOrDefault(1) ?? "—";
    public string Primary6 => V6.ElementAtOrDefault(0) ?? "—";
    public string Secondary6 => V6.ElementAtOrDefault(1) ?? "—";
    public string Description => Loc.Get("Prov_" + Id + "_Desc", "");
    public string Usage => Loc.Get("Prov_" + Id + "_Use", "");
    public string V4Line => $"IPv4  {Primary4}  ·  {Secondary4}";
    public string V6Line => V6.Length == 0 ? "IPv6  —" : $"IPv6  {Primary6}  ·  {Secondary6}";
    public override string ToString() => Name;

    public static readonly IReadOnlyList<DnsProvider> All = new List<DnsProvider>
    {
        new("cloudflare","Cloudflare",new[]{"General","Privacy"},new[]{"1.1.1.1","1.0.0.1"},new[]{"2606:4700:4700::1111","2606:4700:4700::1001"}),
        new("google","Google Public DNS",new[]{"General"},new[]{"8.8.8.8","8.8.4.4"},new[]{"2001:4860:4860::8888","2001:4860:4860::8844"}),
        new("opendns","OpenDNS Home",new[]{"General"},new[]{"208.67.222.222","208.67.220.220"},new[]{"2620:119:35::35","2620:119:53::53"}),
        new("quad9","Quad9",new[]{"Security","Privacy"},new[]{"9.9.9.9","149.112.112.112"},new[]{"2620:fe::fe","2620:fe::9"}),
        new("adguard","AdGuard DNS",new[]{"Security","Privacy"},new[]{"94.140.14.14","94.140.15.15"},new[]{"2a10:50c0::ad1:ff","2a10:50c0::ad2:ff"}),
        new("adguard-nofilter","AdGuard DNS (non-filtering)",new[]{"Privacy"},new[]{"94.140.14.140","94.140.14.141"},new[]{"2a10:50c0::1:ff","2a10:50c0::2:ff"}),
        new("mullvad","Mullvad DNS",new[]{"Privacy"},new[]{"194.242.2.2"},new[]{"2a07:e340::2"}),
        new("cloudflare-security","Cloudflare (Malware blocking)",new[]{"Security"},new[]{"1.1.1.2","1.0.0.2"},new[]{"2606:4700:4700::1112","2606:4700:4700::1002"}),
        new("cleanbrowsing-security","CleanBrowsing Security",new[]{"Security"},new[]{"185.228.168.9","185.228.169.9"},new[]{"2a0d:2a00:1::2","2a0d:2a00:2::2"}),
        new("cloudflare-family","Cloudflare (Family)",new[]{"Family"},new[]{"1.1.1.3","1.0.0.3"},new[]{"2606:4700:4700::1113","2606:4700:4700::1003"}),
        new("opendns-family","OpenDNS FamilyShield",new[]{"Family"},new[]{"208.67.222.123","208.67.220.123"},Array.Empty<string>()),
        new("cleanbrowsing-family","CleanBrowsing Family",new[]{"Family"},new[]{"185.228.168.168","185.228.169.168"},new[]{"2a0d:2a00:1::","2a0d:2a00:2::"}),
        new("adguard-family","AdGuard DNS (Family)",new[]{"Family"},new[]{"94.140.14.15","94.140.15.16"},new[]{"2a10:50c0::bad1:ff","2a10:50c0::bad2:ff"}),
    };
}

public sealed class AdapterInfo
{
    public string Id { get; set; } = ""; public string Name { get; set; } = ""; public string Description { get; set; } = "";
    public AdapterKind Kind { get; set; } public string KindText { get; set; } = ""; public bool IsUp { get; set; }
    public string StatusText { get; set; } = ""; public string Mac { get; set; } = ""; public string IPv4 { get; set; } = "";
    public string IPv6 { get; set; } = ""; public string Gateway { get; set; } = ""; public string Dns { get; set; } = "";
    public string Dhcp { get; set; } = ""; public string Speed { get; set; } = ""; public long SpeedBps { get; set; }
    public int Mtu { get; set; } public int Index4 { get; set; } public int Index6 { get; set; } public bool HasGateway { get; set; }
    public bool HasGlobalV6 { get; set; } public bool IsApipa { get; set; } public bool DnsConfigured { get; set; }
    public string Header => $"{Name}  ·  {StatusText}";
    public List<KeyValue> Rows { get; set; } = new();
}

public sealed class TraceHop
{
    public int Hop { get; set; } public string Address { get; set; } = "*"; public string Hostname { get; set; } = "";
    public string Latency { get; set; } = "—"; public string Status { get; set; } = "";
    public string Line => $"{Hop,2}   {Address,-40} {Latency,8}   {Hostname}";
}

public sealed class TestItem : ObservableObject
{
    private TestState _state; private string _detail = "";
    public string Name { get; set; } = "";
    public TestState State { get => _state; set { Set(ref _state, value); Raise(nameof(StateText)); } }
    public string Detail { get => _detail; set => Set(ref _detail, value); }
    public string Hint { get; set; } = "";
    public string StateText => State switch
    {
        TestState.Passed => "✓ " + Loc.Get("State_Passed", "Passed"),
        TestState.Warning => "⚠ " + Loc.Get("State_Warning", "Warning"),
        _ => "✕ " + Loc.Get("State_Failed", "Failed")
    };
}
