using System.Text.Json;

namespace NetForge.Services;

public sealed class SettingsService
{
    public static SettingsService Instance { get; } = new();
    public string Theme { get; set; } = "system";          // system | light | dark
    public string Language { get; set; } = "system";       // system | en-US | zh-TW | zh-CN | ja-JP | ko-KR
    public bool LowResource { get; set; }
    public double? LastDownMbps { get; set; }
    public double? LastUpMbps { get; set; }

    public static string DataDir
    {
        get { var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetForge"); Directory.CreateDirectory(d); return d; }
    }
    private static string FilePath => Path.Combine(DataDir, "settings.json");

    public void Load()
    {
        try { if (File.Exists(FilePath)) { var s = JsonSerializer.Deserialize<SettingsService>(File.ReadAllText(FilePath)); if (s != null) { Theme = s.Theme; Language = s.Language; LowResource = s.LowResource; LastDownMbps = s.LastDownMbps; LastUpMbps = s.LastUpMbps; } } } catch { }
    }
    public void Save() { try { File.WriteAllText(FilePath, JsonSerializer.Serialize(this)); } catch { } }
}

public static class Logger
{
    public static void Write(Exception ex)
    {
        try { File.AppendAllText(Path.Combine(SettingsService.DataDir, "error.log"), $"[{DateTime.Now:s}] {ex}\n"); } catch { }
    }
}
