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
            {
                var raw = File.ReadAllText(ConfigPath);
                if (ConfigCrypto.IsEncrypted(raw))
                {
                    var decrypted = ConfigCrypto.Decrypt(raw);
                    if (decrypted != null)
                        return JsonConvert.DeserializeObject<DbConnectionConfig>(decrypted);
                }
                else if (!string.IsNullOrWhiteSpace(raw))
                {
                    // Migración: conexión antigua en texto plano -> se re-guarda encriptada.
                    var legacy = JsonConvert.DeserializeObject<DbConnectionConfig>(raw);
                    if (legacy != null)
                    {
                        Save(legacy);
                        return legacy;
                    }
                }
            }
        }
        catch { }
        return null;
    }

    public static void Save(DbConnectionConfig config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(ConfigPath, ConfigCrypto.Encrypt(JsonConvert.SerializeObject(config)));
            if (!OperatingSystem.IsWindows())
            {
                try { File.SetUnixFileMode(ConfigPath, UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch { }
            }
        }
        catch { }
    }
}
