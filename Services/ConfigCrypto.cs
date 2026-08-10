using System.Security.Cryptography;
using System.Text;

namespace Reficio.Services;

public static class ConfigCrypto
{
    public const string Magic = "RFIO1:";
    private const int Version = 1;
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;
    
    public static bool IsEncrypted(string content)
        => !string.IsNullOrEmpty(content) && content.StartsWith(Magic, StringComparison.Ordinal);
    
    public static string Encrypt(string plaintext)
    {
        var keyMaterial = GetKeyMaterial();
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        
        var key = DeriveKey(keyMaterial, salt);
        
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plain, cipher, tag);
        
        var payload = new byte[1 + SaltSize + 12 + 16 + cipher.Length];
        payload[0] = (byte)Version;
        Buffer.BlockCopy(salt, 0, payload, 1, SaltSize);
        Buffer.BlockCopy(nonce, 0, payload, 1 + SaltSize, 12);
        Buffer.BlockCopy(tag, 0, payload, 1 + SaltSize + 12, 16);
        Buffer.BlockCopy(cipher, 0, payload, 1 + SaltSize + 12 + 16, cipher.Length);
        
        return Magic + Convert.ToBase64String(payload);
    }
    
    public static string? Decrypt(string payload)
    {
        try
        {
            if (!IsEncrypted(payload)) return null;
            
            var data = Convert.FromBase64String(payload[Magic.Length..]);
            if (data.Length < 1 + SaltSize + 12 + 16) return null;
            
            var version = data[0];
            if (version != Version) return null;
            
            var salt = data.AsSpan(1, SaltSize);
            var nonce = data.AsSpan(1 + SaltSize, 12);
            var tag = data.AsSpan(1 + SaltSize + 12, 16);
            var cipher = data.AsSpan(1 + SaltSize + 12 + 16);
            
            var key = DeriveKey(GetKeyMaterial(), salt);
            var plain = new byte[cipher.Length];
            
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return null;
        }
    }
    
    private static byte[] GetKeyMaterial()
    {
        // Material base: identificación de máquina/usuario
        var identity = $"reficio|{Environment.MachineName}|{Environment.UserDomainName}|{Environment.UserName}";
        return Encoding.UTF8.GetBytes(identity);
    }
    
    private static byte[] DeriveKey(byte[] keyMaterial, ReadOnlySpan<byte> salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(keyMaterial, salt.ToArray(), Iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeySize);
    }
}