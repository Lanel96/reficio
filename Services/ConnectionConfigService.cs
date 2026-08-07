using Newtonsoft.Json;
using Reficio.Models;

namespace Reficio.Services;

public static class ConnectionConfigService
{
    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".reficio");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "connection.json");

    public static bool Exists()
    {
        try { return File.Exists(ConfigPath) && File.ReadAllText(ConfigPath).Trim().Length > 0; }
        catch { return false; }
    }

    public static DbConnectionConfig? Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
                return JsonConvert.DeserializeObject<DbConnectionConfig>(File.ReadAllText(ConfigPath));
        }
        catch { }
        return null;
    }

    public static void Save(DbConnectionConfig config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(config, Formatting.Indented));
            if (!OperatingSystem.IsWindows())
            {
                try { File.SetUnixFileMode(ConfigPath, UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch { }
            }
        }
        catch { }
    }
}