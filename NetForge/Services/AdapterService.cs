using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetForge.Helpers;
using NetForge.Models;

namespace NetForge.Services;

public sealed class AdapterService : IAdapterService
{
    public IReadOnlyList<AdapterInfo> GetAdapters()
    {
        var list = new List<AdapterInfo>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            try { list.Add(Build(nic)); } catch (Exception ex) { Logger.Write(ex); }
        }
        return list.OrderByDescending(a => a.IsUp).ThenBy(a => a.Kind).ThenBy(a => a.Name).ToList();
    }

    public AdapterInfo? GetPrimary() =>
        GetAdapters().Where(a => a.IsUp && a.HasGateway && a.Kind is not (AdapterKind.Loopback)).OrderBy(a => a.Kind == AdapterKind.Vpn ? 1 : 0).FirstOrDefault();

    private static AdapterKind Classify(NetworkInterface n)
    {
        var d = (n.Description + " " + n.Name).ToLowerInvariant();
        if (n.NetworkInterfaceType == NetworkInterfaceType.Loopback) return AdapterKind.Loopback;
        if (d.Contains("bluetooth")) return AdapterKind.Bluetooth;
        if (d.Contains("vpn") || d.Contains("wireguard") || d.Contains("openvpn") || d.Contains("tap-windows") || d.Contains("wintun") || n.NetworkInterfaceType is NetworkInterfaceType.Ppp or NetworkInterfaceType.Tunnel && !d.Contains("teredo") && !d.Contains("isatap") && !d.Contains("6to4")) return AdapterKind.Vpn;
        if (d.Contains("virtual") || d.Contains("vmware") || d.Contains("hyper-v") || d.Contains("vethernet") || d.Contains("vbox") || d.Contains("teredo") || d.Contains("isatap") || d.Contains("6to4") || d.Contains("pseudo")) return AdapterKind.Virtual;
        if (n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) return AdapterKind.Wifi;
        if (n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetFx or NetworkInterfaceType.FastEthernetT or NetworkInterfaceType.Ethernet3Megabit) return AdapterKind.Ethernet;
        return AdapterKind.Other;
    }

    public static string KindName(AdapterKind k) => k switch
    {
        AdapterKind.Wifi => Loc.Get("Type_Wifi", "Wi-Fi"), AdapterKind.Ethernet => Loc.Get("Type_Ethernet", "Ethernet"),
        AdapterKind.Bluetooth => Loc.Get("Type_Bluetooth", "Bluetooth"), AdapterKind.Vpn => Loc.Get("Type_Vpn", "VPN"),
        AdapterKind.Virtual => Loc.Get("Type_Virtual", "Virtual adapter"), AdapterKind.Loopback => Loc.Get("Type_Loopback", "Loopback"),
        _ => Loc.Get("Type_Other", "Other")
    };

    private static string FormatSpeed(long bps)
    {
        if (bps <= 0 || bps == long.MaxValue) return Loc.Get("Unavailable", "Unavailable");
        return bps >= 1_000_000_000 ? $"{bps / 1_000_000_000.0:0.#} Gbps" : $"{bps / 1_000_000.0:0.#} Mbps";
    }

    private static AdapterInfo Build(NetworkInterface n)
    {
        var props = n.GetIPProperties();
        var a = new AdapterInfo { Id = n.Id, Name = n.Name, Description = n.Description, Kind = Classify(n), IsUp = n.OperationalStatus == OperationalStatus.Up, SpeedBps = n.Speed };
        a.KindText = KindName(a.Kind);
        a.StatusText = a.IsUp ? Loc.Get("Status_Up", "Connected") : Loc.Get("Status_Down", "Disconnected");
        var mac = n.GetPhysicalAddress().ToString();
        a.Mac = mac.Length == 12 ? string.Join("-", Enumerable.Range(0, 6).Select(i => mac.Substring(i * 2, 2))) : Loc.Get("Unavailable", "Unavailable");
        var v4 = props.UnicastAddresses.Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork).Select(u => u.Address).ToList();
        a.IPv4 = v4.Count > 0 ? string.Join(", ", v4) : Loc.Get("Unavailable", "Unavailable");
        a.IsApipa = v4.Any(x => x.GetAddressBytes() is [169, 254, ..]);
        var v6 = props.UnicastAddresses.Where(u => u.Address.AddressFamily == AddressFamily.InterNetworkV6).Select(u => u.Address).ToList();
        var global6 = v6.Where(x => !x.IsIPv6LinkLocal && !x.IsIPv6SiteLocal).ToList();
        a.HasGlobalV6 = global6.Count > 0;
        a.IPv6 = global6.Count > 0 ? global6[0].ToString() : v6.Count > 0 ? v6[0].ToString().Split('%')[0] : Loc.Get("Unavailable", "Unavailable");
        var gws = props.GatewayAddresses.Select(g => g.Address).Where(x => !x.Equals(IPAddress.Any) && !x.Equals(IPAddress.IPv6Any)).ToList();
        a.HasGateway = gws.Count > 0;
        a.Gateway = gws.Count > 0 ? string.Join(", ", gws) : Loc.Get("Unavailable", "Unavailable");
        a.DnsConfigured = props.DnsAddresses.Count > 0;
        a.Dns = props.DnsAddresses.Count > 0 ? string.Join(", ", props.DnsAddresses.Select(d => d.ToString().Split('%')[0])) : Loc.Get("Unavailable", "Unavailable");
        a.Speed = FormatSpeed(n.Speed);
        try { var p4 = props.GetIPv4Properties(); a.Index4 = p4.Index; a.Mtu = p4.Mtu; a.Dhcp = p4.IsDhcpEnabled ? Loc.Get("On", "On") : Loc.Get("Off", "Off"); } catch { a.Dhcp = Loc.Get("Unavailable", "Unavailable"); }
        try { a.Index6 = props.GetIPv6Properties().Index; } catch { }
        a.Rows = new List<KeyValue>
        {
            new(Loc.Get("Lbl_Adapter", "Adapter"), n.Description), new(Loc.Get("Lbl_Status", "Status"), a.StatusText),
            new(Loc.Get("Lbl_Speed", "Link speed"), a.Speed), new(Loc.Get("Lbl_Mac", "MAC address"), a.Mac),
            new("IPv4", a.IPv4), new("IPv6", a.IPv6), new(Loc.Get("Lbl_Gateway", "Gateway"), a.Gateway),
            new("DNS", a.Dns), new("DHCP", a.Dhcp)
        };
        return a;
    }
}
