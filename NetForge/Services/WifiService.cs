using System.Runtime.InteropServices;
using NetForge.Helpers;
using NetForge.Models;

namespace NetForge.Services;

/// <summary>Reads the current Wi-Fi connection through the native WLAN API (wlanapi.dll), so values don't depend on the Windows display language.</summary>
public sealed class WifiService : IWifiService
{
    [DllImport("wlanapi.dll")] static extern uint WlanOpenHandle(uint ver, IntPtr res, out uint negotiated, out IntPtr handle);
    [DllImport("wlanapi.dll")] static extern uint WlanCloseHandle(IntPtr h, IntPtr res);
    [DllImport("wlanapi.dll")] static extern uint WlanEnumInterfaces(IntPtr h, IntPtr res, out IntPtr list);
    [DllImport("wlanapi.dll")] static extern uint WlanQueryInterface(IntPtr h, ref Guid g, int opcode, IntPtr res, out uint size, out IntPtr data, IntPtr type);
    [DllImport("wlanapi.dll")] static extern uint WlanGetNetworkBssList(IntPtr h, ref Guid g, IntPtr ssid, int bssType, [MarshalAs(UnmanagedType.Bool)] bool secure, IntPtr res, out IntPtr list);
    [DllImport("wlanapi.dll")] static extern void WlanFreeMemory(IntPtr p);

    public Task<List<KeyValue>?> GetAsync() => Task.Run(Query);

    private static string Phy(uint p) => p switch { 4 => "802.11a", 5 => "802.11b", 6 => "802.11g", 7 => "802.11n (Wi-Fi 4)", 8 => "802.11ac (Wi-Fi 5)", 9 => "802.11ad", 10 => "802.11ax (Wi-Fi 6/6E)", 11 => "802.11be (Wi-Fi 7)", _ => "" };
    private static string Auth(uint a) => a switch { 1 => "Open", 2 => "Shared key", 3 => "WPA", 4 => "WPA-Personal", 5 => "WPA-None", 6 => "WPA2-Enterprise", 7 => "WPA2-Personal", 8 => "WPA3-Enterprise 192-bit", 9 => "WPA3-Personal (SAE)", 10 => "OWE", 11 => "WPA3-Enterprise", _ => "" };
    private static string Cipher(uint c) => c switch { 0 => "None", 1 => "WEP-40", 2 => "TKIP", 4 => "CCMP (AES)", 5 => "WEP-104", 6 => "GCMP", 9 => "GCMP-256", _ => "" };

    private List<KeyValue>? Query()
    {
        var na = Loc.Get("Unavailable", "Unavailable");
        IntPtr h = IntPtr.Zero;
        try
        {
            if (WlanOpenHandle(2, IntPtr.Zero, out _, out h) != 0) return null;
            if (WlanEnumInterfaces(h, IntPtr.Zero, out var list) != 0) return null;
            try
            {
                int count = Marshal.ReadInt32(list);
                for (int i = 0; i < count; i++)
                {
                    var item = IntPtr.Add(list, 8 + i * 532);
                    var guidBytes = new byte[16]; Marshal.Copy(item, guidBytes, 0, 16); var guid = new Guid(guidBytes);
                    var state = Marshal.ReadInt32(item, 528);
                    if (state != 1) continue;   // wlan_interface_state_connected
                    if (WlanQueryInterface(h, ref guid, 7, IntPtr.Zero, out _, out var data, IntPtr.Zero) != 0) continue;
                    try
                    {
                        uint ssidLen = (uint)Marshal.ReadInt32(data, 520);
                        var ssidBytes = new byte[Math.Min(ssidLen, 32)]; Marshal.Copy(IntPtr.Add(data, 524), ssidBytes, 0, ssidBytes.Length);
                        var ssid = System.Text.Encoding.UTF8.GetString(ssidBytes);
                        var bssid = new byte[6]; Marshal.Copy(IntPtr.Add(data, 560), bssid, 0, 6);
                        uint phy = (uint)Marshal.ReadInt32(data, 568);
                        uint quality = (uint)Marshal.ReadInt32(data, 576);
                        uint rx = (uint)Marshal.ReadInt32(data, 580), tx = (uint)Marshal.ReadInt32(data, 584);
                        bool secEnabled = Marshal.ReadInt32(data, 588) != 0;
                        uint auth = (uint)Marshal.ReadInt32(data, 596), cipher = (uint)Marshal.ReadInt32(data, 600);
                        string signal = $"{quality}%", channel = na, freq = na;
                        try
                        {
                            var ssidStruct = Marshal.AllocHGlobal(36);
                            try
                            {
                                Marshal.WriteInt32(ssidStruct, (int)ssidLen); Marshal.Copy(ssidBytes, 0, IntPtr.Add(ssidStruct, 4), ssidBytes.Length);
                                if (WlanGetNetworkBssList(h, ref guid, ssidStruct, 1, secEnabled, IntPtr.Zero, out var bss) == 0)
                                {
                                    try
                                    {
                                        int n = Marshal.ReadInt32(bss, 4); IntPtr best = IntPtr.Zero; int bestRssi = int.MinValue;
                                        for (int j = 0; j < n; j++)
                                        {
                                            var e = IntPtr.Add(bss, 8 + j * 360);
                                            var eb = new byte[6]; Marshal.Copy(IntPtr.Add(e, 40), eb, 0, 6);
                                            int rssi = Marshal.ReadInt32(e, 56);
                                            if (eb.SequenceEqual(bssid)) { best = e; break; }
                                            if (rssi > bestRssi) { bestRssi = rssi; best = e; }
                                        }
                                        if (best != IntPtr.Zero)
                                        {
                                            uint khz = (uint)Marshal.ReadInt32(best, 92);
                                            int ch = khz switch { 2484000 => 14, < 2484000 and > 2400000 => (int)((khz - 2407000) / 5000), >= 5955000 and < 7125001 => (int)((khz - 5950000) / 5000), > 4900000 and < 5955000 => (int)((khz - 5000000) / 5000), _ => 0 };
                                            if (ch > 0) channel = ch.ToString();
                                            freq = khz >= 5955000 ? $"{khz / 1000.0 / 1000.0:0.###} GHz (6 GHz)" : $"{khz / 1000.0 / 1000.0:0.###} GHz";
                                            signal += $"  ({Marshal.ReadInt32(best, 56)} dBm)";
                                        }
                                    }
                                    finally { WlanFreeMemory(bss); }
                                }
                            }
                            finally { Marshal.FreeHGlobal(ssidStruct); }
                        }
                        catch { }
                        string Or(string s) => string.IsNullOrEmpty(s) ? na : s;
                        return new List<KeyValue>
                        {
                            new("SSID", Or(ssid)), new("BSSID", string.Join(":", bssid.Select(b => b.ToString("X2")))),
                            new(Loc.Get("Lbl_Signal", "Signal"), signal), new(Loc.Get("Lbl_Channel", "Channel"), channel), new(Loc.Get("Lbl_Frequency", "Frequency"), freq),
                            new(Loc.Get("Lbl_Radio", "Radio type"), Or(Phy(phy))), new(Loc.Get("Lbl_Rx", "Receive rate"), rx > 0 ? $"{rx / 1000.0:0.#} Mbps" : na),
                            new(Loc.Get("Lbl_Tx", "Transmit rate"), tx > 0 ? $"{tx / 1000.0:0.#} Mbps" : na),
                            new(Loc.Get("Lbl_Auth", "Authentication"), Or(Auth(auth))), new(Loc.Get("Lbl_Encryption", "Encryption"), Or(Cipher(cipher)))
                        };
                    }
                    finally { WlanFreeMemory(data); }
                }
            }
            finally { WlanFreeMemory(list); }
            return null;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or SEHException) { return null; }
        catch (Exception ex) { Logger.Write(ex); return null; }
        finally { if (h != IntPtr.Zero) WlanCloseHandle(h, IntPtr.Zero); }
    }
}
