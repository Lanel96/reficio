using System.Security.Cryptography;
using System.Text;

namespace Reficio.Services;

public static class ConfigCrypto
{
    public const string Magic = "RFIO1:";

    public static bool IsEncrypted(string content)
        => !string.IsNullOrEmpty(content) && content.StartsWith(Magic, StringComparison.Ordinal);

    public static string Encrypt(string plaintext)
    {
        var key = GetKey();
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];

        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plain, cipher, tag);

        var payload = new byte[12 + 16 + cipher.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, 12);
        Buffer.BlockCopy(tag, 0, payload, 12, 16);
        Buffer.BlockCopy(cipher, 0, payload, 28, cipher.Length);

        return Magic + Convert.ToBase64String(payload);
    }

    public static string? Decrypt(string payload)
    {
        try
        {
            if (!IsEncrypted(payload)) return null;

            var data = Convert.FromBase64String(payload[Magic.Length..]);
            if (data.Length < 12 + 16) return null;

            var nonce = data.AsSpan(0, 12);
            var tag = data.AsSpan(12, 16);
            var cipher = data.AsSpan(28);

            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(GetKey(), 16);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] GetKey()
    {
        // Clave derivada del usuario y la máquina: el archivo encriptado solo
        // se descifra en la misma cuenta/máquina donde fue guardado.
        var identity = $"reficio|{Environment.MachineName}|{Environment.UserDomainName}|{Environment.UserName}";
        return SHA256.HashData(Encoding.UTF8.GetBytes(identity));
    }
}
