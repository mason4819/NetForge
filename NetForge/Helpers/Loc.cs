using Microsoft.Windows.ApplicationModel.Resources;

namespace NetForge.Helpers;

/// <summary>Resource lookup. Falls back to the supplied English text if a key is missing in the current language.</summary>
public static class Loc
{
    private static ResourceLoader? _loader;
    private static ResourceLoader Loader => _loader ??= new ResourceLoader();

    public static string Get(string key, string? fallback = null)
    {
        try { var s = Loader.GetString(key); if (!string.IsNullOrEmpty(s)) return s; } catch { }
        return fallback ?? key;
    }
    public static string F(string key, string fallback, params object[] args) => string.Format(Get(key, fallback), args);
}
