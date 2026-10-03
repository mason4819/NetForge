using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using NetForge.Helpers;

namespace NetForge.Services;

public enum FieldKind { Text, Ip, Int, Host, Adapter, Choice }

public sealed record AdvField(string Key, string LabelEn, FieldKind Kind = FieldKind.Text, string Default = "", int Min = 0, int Max = 0, string[]? Options = null)
{
    public string Label => Loc.Get("Fld_" + Key, LabelEn);
}

public sealed record AdvAction(string Id, string LabelEn, string Template, bool Admin = false, string WarnEn = "", AdvField[]? Fields = null, string NoteEn = "")
{
    public string Label => Loc.Get("Act_" + Id, LabelEn);
    public string Warning => string.IsNullOrEmpty(WarnEn) ? "" : Loc.Get("Warn_" + Id, WarnEn);
    public string Note => string.IsNullOrEmpty(NoteEn) ? "" : Loc.Get("Note_" + Id, NoteEn);
    public AdvField[] FieldList => Fields ?? Array.Empty<AdvField>();

    /// <summary>Builds the final PowerShell. With validate=true bad input throws FormatException; with false it's only for the live preview.</summary>
    public string Build(IDictionary<string, string> values, bool validate)
    {
        var s = Template;
        foreach (var f in FieldList)
        {
            values.TryGetValue(f.Key, out var v); v = (v ?? "").Trim();
            if (validate)
            {
                bool ok = f.Kind switch
                {
                    FieldKind.Ip => IPAddress.TryParse(v, out _),
                    FieldKind.Int => int.TryParse(v, out var n) && n >= f.Min && n <= f.Max,
                    FieldKind.Host => Regex.IsMatch(v, @"^[A-Za-z0-9._-]{1,253}$"),
                    FieldKind.Adapter => NetworkInterface.GetAllNetworkInterfaces().Any(i => i.Name == v),
                    FieldKind.Choice => f.Options?.Contains(v) == true,
                    _ => v.Length > 0 && !v.Contains('\n') && !v.Contains('\r')
                };
                if (!ok) throw new FormatException(f.Label);
            }
            s = s.Replace("{" + f.Key + "}", v.Replace("'", "''"));
        }
        return s;
    }
}

public sealed record AdvSection(string Key, string? ReadScript, AdvAction[] Actions, string TipEn = "")
{
    public string Tip => string.IsNullOrEmpty(TipEn) ? "" : Loc.Get("Tip_" + Key, TipEn);
}

/// <summary>Every Advanced page is data: one read-only view script plus a few explicit actions. Nothing runs until the user confirms an action.</summary>
public static class AdvancedCatalog
{
    private static readonly AdvField Adapter = new("adapter", "Adapter", FieldKind.Adapter);
    private static readonly AdvField Ip = new("ip", "IP address", FieldKind.Ip);
    private static readonly AdvField Gateway = new("gateway", "Gateway", FieldKind.Ip);
    private static readonly AdvField Metric = new("metric", "Metric", FieldKind.Int, "25", 1, 9999);

    private const string HostsPath = "$p = Join-Path $env:SystemRoot 'System32\\drivers\\etc\\hosts'; $b = Join-Path $env:ProgramData 'NetForge'; New-Item -ItemType Directory -Force $b | Out-Null; Copy-Item $p (Join-Path $b ('hosts.' + (Get-Date -Format yyyyMMdd-HHmmss) + '.bak')); ";

    public static AdvSection Get(string key) => Sections[key];

    public static readonly Dictionary<string, AdvSection> Sections = new()
    {
        ["ipv4"] = new("ipv4", """
            Get-NetIPConfiguration | ForEach-Object {
              $i = Get-NetIPInterface -InterfaceIndex $_.InterfaceIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue
              '[' + $_.InterfaceAlias + ']'
              '  IPv4     : ' + (($_.IPv4Address | ForEach-Object { $_.IPAddress + '/' + $_.PrefixLength }) -join ', ')
              '  Gateway  : ' + ($_.IPv4DefaultGateway.NextHop -join ', ')
              '  DNS      : ' + ($_.DNSServer.ServerAddresses -join ', ')
              '  DHCP     : ' + $i.Dhcp
              '  Metric   : ' + $i.InterfaceMetric + ' (automatic: ' + $i.AutomaticMetric + ')'
              ''
            }
            """, new[]
        {
            new AdvAction("ipv4_dhcp", "Use DHCP", """
                $a='{adapter}'
                Set-NetIPInterface -InterfaceAlias $a -AddressFamily IPv4 -Dhcp Enabled
                Get-NetIPAddress -InterfaceAlias $a -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object PrefixOrigin -eq 'Manual' | Remove-NetIPAddress -Confirm:$false
                Get-NetRoute -InterfaceAlias $a -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | Where-Object Protocol -eq 'NetMgmt' | Remove-NetRoute -Confirm:$false
                ipconfig /renew $a | Out-Null
                'OK'
                """, true, "This replaces any static IPv4 address on that adapter with DHCP.", new[] { Adapter }),
            new AdvAction("ipv4_static", "Set static IP", """
                $a='{adapter}'
                Set-NetIPInterface -InterfaceAlias $a -AddressFamily IPv4 -Dhcp Disabled
                Get-NetIPAddress -InterfaceAlias $a -AddressFamily IPv4 -ErrorAction SilentlyContinue | Remove-NetIPAddress -Confirm:$false
                Get-NetRoute -InterfaceAlias $a -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | Remove-NetRoute -Confirm:$false
                New-NetIPAddress -InterfaceAlias $a -IPAddress '{ip}' -PrefixLength {prefix} -DefaultGateway '{gateway}' | Out-Null
                'OK'
                """, true, "A wrong address or gateway can cut this PC off the network. You can switch back with 'Use DHCP'.",
                new[] { Adapter, Ip, new AdvField("prefix", "Prefix length (24 = 255.255.255.0)", FieldKind.Int, "24", 1, 32), Gateway }),
            new AdvAction("ipv4_metric", "Set interface metric", "Set-NetIPInterface -InterfaceAlias '{adapter}' -AddressFamily IPv4 -AutomaticMetric Disabled -InterfaceMetric {metric}; 'OK'", true, "", new[] { Adapter, Metric }),
            new AdvAction("ipv4_autometric", "Automatic metric", "Set-NetIPInterface -InterfaceAlias '{adapter}' -AddressFamily IPv4 -AutomaticMetric Enabled; 'OK'", true, "", new[] { Adapter }),
        }),

        ["ipv6"] = new("ipv6", """
            Get-NetIPConfiguration | ForEach-Object {
              $i = Get-NetIPInterface -InterfaceIndex $_.InterfaceIndex -AddressFamily IPv6 -ErrorAction SilentlyContinue
              '[' + $_.InterfaceAlias + ']'
              '  IPv6     : ' + (($_.IPv6Address | ForEach-Object { $_.IPAddress + '/' + $_.PrefixLength }) -join ', ')
              '  Gateway  : ' + ($_.IPv6DefaultGateway.NextHop -join ', ')
              '  DNS      : ' + (($_.DNSServer | Where-Object AddressFamily -eq 23).ServerAddresses -join ', ')
              '  DHCPv6   : ' + $i.Dhcp
              '  Metric   : ' + $i.InterfaceMetric + ' (automatic: ' + $i.AutomaticMetric + ')'
              ''
            }
            """, new[]
        {
            new AdvAction("ipv6_metric", "Set interface metric", "Set-NetIPInterface -InterfaceAlias '{adapter}' -AddressFamily IPv6 -AutomaticMetric Disabled -InterfaceMetric {metric}; 'OK'", true, "", new[] { Adapter, Metric }),
            new AdvAction("ipv6_autometric", "Automatic metric", "Set-NetIPInterface -InterfaceAlias '{adapter}' -AddressFamily IPv6 -AutomaticMetric Enabled; 'OK'", true, "", new[] { Adapter }),
        }),

        ["tcpip"] = new("tcpip", """
            'TCP global parameters'; netsh int tcp show global
            'TCP connections by state'; Get-NetTCPConnection | Group-Object State | Sort-Object Count -Descending | Format-Table Count, Name -AutoSize
            'Established (first 60)'; Get-NetTCPConnection -State Established | Select-Object -First 60 LocalAddress, LocalPort, RemoteAddress, RemotePort, OwningProcess | Format-Table -AutoSize
            'UDP endpoints: ' + @(Get-NetUDPEndpoint).Count
            """, Array.Empty<AdvAction>()),

        ["dhcp"] = new("dhcp", "ipconfig /all", new[]
        {
            new AdvAction("dhcp_release", "Release", "ipconfig /release | Out-Null; 'OK'", true, "Releasing drops your IP address, so you'll be offline until you renew."),
            new AdvAction("dhcp_renew", "Renew", "ipconfig /renew | Out-Null; 'OK'", true),
        }, "Search for 'DHCP' or 'Lease' to jump to the interesting lines."),

        ["routing"] = new("routing", "route print", new[]
        {
            new AdvAction("route_add", "Add route", "route add {dest} mask {mask} {gateway} metric {metric}", true, "A bad route can send traffic into a black hole. Not permanent unless you know otherwise.",
                new[] { new AdvField("dest", "Destination", FieldKind.Ip), new AdvField("mask", "Netmask", FieldKind.Ip, "255.255.255.0"), Gateway, Metric }),
            new AdvAction("route_del", "Remove route", "route delete {dest}", true, "Removing the wrong route can drop your connection.", new[] { new AdvField("dest", "Destination", FieldKind.Ip) }),
        }),

        ["arp"] = new("arp", "Get-NetNeighbor -AddressFamily IPv4 | Sort-Object InterfaceAlias, IPAddress | Format-Table IPAddress, LinkLayerAddress, State, InterfaceAlias -AutoSize", new[]
        {
            new AdvAction("arp_clear", "Clear ARP cache", "netsh interface ip delete arpcache; 'OK'", true, "Harmless. Windows just relearns the entries as needed."),
        }),

        ["mtu"] = new("mtu", "Get-NetIPInterface | Sort-Object InterfaceAlias | Format-Table InterfaceAlias, AddressFamily, NlMtu, ConnectionState -AutoSize", new[]
        {
            new AdvAction("mtu_set", "Set MTU", "& netsh interface ipv4 set subinterface '{adapter}' mtu={mtu} store=persistent", true,
                "Changing MTU can make your network throw a tantrum. If you don't know why you're changing it, leave it alone.", new[] { Adapter, new AdvField("mtu", "MTU", FieldKind.Int, "1500", 576, 9000) }),
            new AdvAction("mtu_reset", "Back to 1500", "& netsh interface ipv4 set subinterface '{adapter}' mtu=1500 store=persistent", true, "", new[] { Adapter }),
        }, "Not sure what this is? It sets how big one network packet can be. If you have no special reason, 1500 is usually the right number."),

        ["proxy"] = new("proxy", """
            $k = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings'
            $v = Get-ItemProperty $k
            'Proxy enabled : ' + [bool]$v.ProxyEnable
            'Server        : ' + $v.ProxyServer
            'Auto-detect   : ' + $(if ($v.AutoDetect -eq $null) { '(not set)' } else { [bool]$v.AutoDetect })
            'PAC script    : ' + $v.AutoConfigURL
            ''
            'WinHTTP:'; netsh winhttp show proxy
            """, new[]
        {
            new AdvAction("proxy_on", "Enable proxy", "$k='HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings'; Set-ItemProperty $k ProxyServer '{address}:{port}'; Set-ItemProperty $k ProxyEnable 1; 'OK'", false,
                "All apps that follow the system proxy will send traffic through this server.", new[] { new AdvField("address", "Address", FieldKind.Host), new AdvField("port", "Port", FieldKind.Int, "8080", 1, 65535) },
                "Only affects your user account. No admin needed."),
            new AdvAction("proxy_off", "Disable proxy", "Set-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings' ProxyEnable 0; 'OK'", false),
        }),

        ["hosts"] = new("hosts", "Get-Content (Join-Path $env:SystemRoot 'System32\\drivers\\etc\\hosts')", new[]
        {
            new AdvAction("hosts_add", "Add / replace entry",
                HostsPath + "$lines = @(Get-Content $p | Where-Object { $_ -notmatch ('^\\s*[^#\\s]+\\s+' + [regex]::Escape('{host}') + '(\\s|$)') }); $lines += \"{ip}`t{host}\"; Set-Content -Path $p -Value $lines -Encoding ASCII; 'OK'",
                true, "This edits the hosts file, which Windows checks before DNS. A backup is made first.", new[] { Ip, new AdvField("host", "Hostname", FieldKind.Host) },
                "The preview shows exactly what will run."),
            new AdvAction("hosts_del", "Delete entry",
                HostsPath + "$lines = @(Get-Content $p | Where-Object { $_ -notmatch ('^\\s*[^#\\s]+\\s+' + [regex]::Escape('{host}') + '(\\s|$)') }); Set-Content -Path $p -Value $lines -Encoding ASCII; 'OK'",
                true, "Removes every non-comment line for that hostname. A backup is made first.", new[] { new AdvField("host", "Hostname", FieldKind.Host) }),
            new AdvAction("hosts_backup", "Backup", HostsPath + "'Backup saved in ' + $b", true),
            new AdvAction("hosts_restore", "Restore latest backup",
                "$p = Join-Path $env:SystemRoot 'System32\\drivers\\etc\\hosts'; $b = Join-Path $env:ProgramData 'NetForge'; $f = Get-ChildItem (Join-Path $b 'hosts.*.bak') -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1; if (-not $f) { throw 'No hosts backup found.' }; Copy-Item $f.FullName $p -Force; 'Restored ' + $f.Name",
                true, "Replaces the current hosts file with the most recent backup."),
        }, "Edit = 'Add / replace entry'. Use the search box to filter lines."),

        ["firewall"] = new("firewall", "Get-NetFirewallProfile | Format-Table Name, Enabled, DefaultInboundAction, DefaultOutboundAction -AutoSize", new[]
        {
            new AdvAction("fw_on", "Turn on", "Set-NetFirewallProfile -Profile '{profile}' -Enabled True; 'OK'", true, "", new[] { new AdvField("profile", "Profile", FieldKind.Choice, "Public", Options: new[] { "Domain", "Private", "Public" }) }),
            new AdvAction("fw_off", "Turn off", "Set-NetFirewallProfile -Profile '{profile}' -Enabled False; 'OK'", true,
                "Security warning: with the firewall off, other devices on this network can reach services on your PC that are normally blocked. Only do this to test something, and turn it back on afterwards.",
                new[] { new AdvField("profile", "Profile", FieldKind.Choice, "Public", Options: new[] { "Domain", "Private", "Public" }) }),
        }),

        ["winsock"] = new("winsock", "netsh winsock show catalog | Select-Object -First 60", new[]
        {
            new AdvAction("winsock_reset", "Reset Winsock", "netsh winsock reset", true, "This may need a Windows restart to take effect. Windows: \"I might need a nap.\""),
        }),

        ["netbios"] = new("netbios", """
            Get-CimInstance Win32_NetworkAdapterConfiguration -Filter 'IPEnabled=true' | ForEach-Object {
              $m = switch ($_.TcpipNetbiosOptions) { 0 {'Default (from DHCP)'} 1 {'Enabled'} 2 {'Disabled'} default {'Unknown'} }
              $_.Description + '  ->  NetBIOS over TCP/IP: ' + $m
            }
            ''
            nbtstat -n
            """, Array.Empty<AdvAction>(), "View only. Windows doesn't expose a safe, general way to change this from here."),

        ["qos"] = new("qos", """
            'QoS policies:'; $q = Get-NetQosPolicy -ErrorAction SilentlyContinue; if ($q) { $q | Format-Table Name, AppPathNameMatchCondition, DSCPAction, ThrottleRateActionBitsPerSecond -AutoSize } else { '  (none)' }
            ''
            'Adapter QoS:'; Get-NetAdapterQos -ErrorAction SilentlyContinue | Format-Table Name, Enabled -AutoSize
            ''
            'TCP global:'; netsh int tcp show global
            """, Array.Empty<AdvAction>(), "View only."),

        ["reset"] = new("reset", null, new[]
        {
            new AdvAction("reset_dns", "Flush DNS cache", "Clear-DnsClientCache; 'DNS cache cleared.'", true, "Harmless. The cache just refills."),
            new AdvAction("reset_winsock", "Reset Winsock", "netsh winsock reset", true, "May need a restart."),
            new AdvAction("reset_tcpip", "Reset TCP/IP", "netsh int ip reset; netsh int ipv6 reset", true, "This resets the TCP/IP stack to defaults, including static IP settings. A restart is needed."),
            new AdvAction("reset_release", "Release DHCP", "ipconfig /release | Out-Null; 'OK'", true, "You'll be offline until you renew."),
            new AdvAction("reset_renew", "Renew DHCP", "ipconfig /renew | Out-Null; 'OK'", true),
            new AdvAction("reset_adapter", "Restart adapter", "Restart-NetAdapter -Name '{adapter}' -Confirm:$false; 'OK'", true, "The adapter drops for a few seconds.", new[] { Adapter }),
        }, "Each of these is its own thing. Pick only the one you need."),
    };
}
