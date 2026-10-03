using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using NetForge.Helpers;
using NetForge.Models;

namespace NetForge.Services;

public static class ProcessRunner
{
    public static async Task<(int Code, string Output)> RunAsync(string file, string args, CancellationToken ct = default)
    {
        var enc = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        var psi = new ProcessStartInfo(file, args) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = enc, StandardErrorEncoding = enc };
        using var p = Process.Start(psi)!;
        var o = p.StandardOutput.ReadToEndAsync(ct); var e = p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        return (p.ExitCode, (await o) + (await e));
    }

    private static string Encode(string script) => Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
    private const string Prefix = "[Console]::OutputEncoding=[Text.Encoding]::UTF8;$ProgressPreference='SilentlyContinue';";

    public static async Task<(int Code, string Output)> RunPowerShellAsync(string script, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + Encode(Prefix + script))
        { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        using var p = Process.Start(psi)!;
        var o = p.StandardOutput.ReadToEndAsync(ct); var e = p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        return (p.ExitCode, (await o) + (await e));
    }

    /// <summary>Runs a script through a one-shot UAC prompt. Output comes back via a temp file because elevated processes can't be redirected.</summary>
    public static async Task<(int Code, string Output)> RunPowerShellElevatedAsync(string script, CancellationToken ct = default)
    {
        var tmp = Path.Combine(Path.GetTempPath(), "netforge-" + Guid.NewGuid().ToString("N") + ".txt");
        var wrapped = Prefix + "$ErrorActionPreference='Stop';try{ & { " + script + " } *>&1 | Out-String -Width 220 | Set-Content -Path '" + tmp + "' -Encoding UTF8; exit 0 }catch{ ($_ | Out-String) | Set-Content -Path '" + tmp + "' -Encoding UTF8; exit 1 }";
        var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + Encode(wrapped))
        { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
        int code;
        try { using var p = Process.Start(psi)!; await p.WaitForExitAsync(ct); code = p.ExitCode; }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { return (-1223, ""); }
        string output = "";
        try { if (File.Exists(tmp)) { output = File.ReadAllText(tmp); File.Delete(tmp); } } catch { }
        return (code, output);
    }
}

/// <summary>Single entry point for running system commands. Only asks for UAC when the action really needs it.</summary>
public static class Sys
{
    public static async Task<string> ReadAsync(string script, CancellationToken ct = default)
    {
        var (_, o) = await ProcessRunner.RunPowerShellAsync("& { " + script + " } 2>&1 | Out-String -Width 220", ct);
        return o.TrimEnd();
    }

    public static async Task<OpResult> RunAsync(string script, bool admin, CancellationToken ct = default)
    {
        if (admin && !Elevation.IsAdmin)
        {
            var (c, o) = await ProcessRunner.RunPowerShellElevatedAsync(script, ct);
            if (c == -1223) return new OpResult(false, Loc.Get("Msg_UacDenied", "Windows didn't get the admin approval, so nothing was changed."), true);
            return new OpResult(c == 0, o.Trim());
        }
        var (c2, o2) = await ProcessRunner.RunPowerShellAsync("$ErrorActionPreference='Stop';try{ & { " + script + " } *>&1 | Out-String -Width 220; exit 0 }catch{ ($_ | Out-String); exit 1 }", ct);
        return new OpResult(c2 == 0, o2.Trim());
    }
}
