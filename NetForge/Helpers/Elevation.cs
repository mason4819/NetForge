using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using Microsoft.UI.Xaml;

namespace NetForge.Helpers;

public static class Elevation
{
    public static bool IsAdmin
    {
        get { using var id = WindowsIdentity.GetCurrent(); return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator); }
    }

    /// <summary>Restarts NetForge with a UAC prompt. Only called when the user clicks the button.</summary>
    public static void RestartAsAdmin()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" });
            Application.Current.Exit();
        }
        catch (Win32Exception) { /* user cancelled UAC */ }
    }
}
