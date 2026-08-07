using Newtonsoft.Json;
using Reficio.Models;

namespace Reficio.Services;

public static class ConfigService
{
    private static readonly string ConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".reficio");
    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    public static AppConfig Load()
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
                        return JsonConvert.DeserializeObject<AppConfig>(decrypted) ?? new AppConfig();
                }
                else if (!string.IsNullOrWhiteSpace(raw))
                {
                    // Migración: config antigua en texto plano -> se re-guarda encriptada.
                    var legacy = JsonConvert.DeserializeObject<AppConfig>(raw);
                    if (legacy != null)
                    {
                        Save(legacy);
                        return legacy;
                    }
                }
            }
        }
        catch { }
        return new AppConfig();
    }

    public static void Save(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(ConfigPath, ConfigCrypto.Encrypt(JsonConvert.SerializeObject(config)));
        }
        catch { }
    }
}
