using BCrypt.Net;
using Reficio.Models;

namespace Reficio.Services;

public static class AuthService
{
    public static AuthResult Authenticate(FirebirdDbService db, string username, string password)
    {
        username = username.Trim();
        if (string.IsNullOrEmpty(username)) return new AuthResult { Reason = "Ingrese el usuario" };
        if (string.IsNullOrEmpty(password)) return new AuthResult { Reason = "Ingrese la contraseña" };
        
        QueryResult result;
        try
        {
            result = db.Query("SELECT USUA, PASE, DEPA FROM MUSUA WHERE TRIM(USUA) = @p0", username);
        }
        catch (Exception ex)
        {
            return new AuthResult { Reason = $"No se pudo consultar MUSUA: {ex.Message}" };
        }
        
        string? storedHash = null;
        string? storedDept = null;
        if (result.Rows.Count > 0)
        {
            var row = result.Rows[0];
            storedHash = row.TryGetValue("PASE", out var p) ? p?.ToString()?.Trim() : "";
            storedDept = row.TryGetValue("DEPA", out var d) ? d?.ToString()?.Trim() : "";
        }
        
        return Evaluate(username, password, storedHash, storedDept);
    }
    
    public static AuthResult Evaluate(string username, string password, string? storedHash, string? storedDept)
    {
        var trimmedUser = username?.Trim() ?? "";
        var trimmedPass = password ?? "";
        if (string.IsNullOrEmpty(trimmedUser)) return new AuthResult { Reason = "Ingrese el usuario" };
        if (string.IsNullOrEmpty(trimmedPass)) return new AuthResult { Reason = "Ingrese la contraseña" };
        if (string.IsNullOrEmpty(storedHash)) return new AuthResult { Reason = "Usuario o contraseña incorrectos" };
        
        var dept = storedDept?.Trim() ?? "";
        
        bool passwordValid;
        try
        {
            if (storedHash.StartsWith("$2a$") || storedHash.StartsWith("$2b$") || storedHash.StartsWith("$2y$"))
            {
                passwordValid = BCrypt.Net.BCrypt.Verify(trimmedPass, storedHash);
            }
            else
            {
                passwordValid = string.Equals(storedHash.Trim(), trimmedPass, StringComparison.Ordinal);
            }
        }
        catch
        {
            return new AuthResult { Reason = "Error al verificar contraseña" };
        }
        
        if (!passwordValid)
            return new AuthResult { Reason = "Usuario o contraseña incorrectos" };
        
        if (!dept.Equals("sistemas", StringComparison.OrdinalIgnoreCase))
            return new AuthResult { Reason = "Acceso restringido: solo usuarios del departamento Sistemas" };
        
        var user = new UserModel { Usuario = trimmedUser, Departamento = dept };
        return new AuthResult { Success = true, User = user };
    }
    
    public static string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("La contraseña no puede estar vacía", nameof(password));
        var salt = BCrypt.Net.BCrypt.GenerateSalt(12);
        return BCrypt.Net.BCrypt.HashPassword(password, salt);
    }
    
    public static bool NeedsRehash(string hash)
    {
        if (string.IsNullOrEmpty(hash)) return false;
        try
        {
            // PasswordNeedsRehash requiere newMinimumWorkLoad en BCrypt.Net-Next 4.x
            return BCrypt.Net.BCrypt.PasswordNeedsRehash(hash, 12);
        }
        catch
        {
            return true;
        }
    }
}