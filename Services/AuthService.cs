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

        string? storedPassword = null;
        string? storedDept = null;
        if (result.Rows.Count > 0)
        {
            var row = result.Rows[0];
            storedPassword = row.TryGetValue("PASE", out var p) ? p?.ToString()?.Trim() : "";
            storedDept = row.TryGetValue("DEPA", out var d) ? d?.ToString()?.Trim() : "";
        }

        return Evaluate(username, password, storedPassword, storedDept);
    }

    public static AuthResult Evaluate(string username, string password, string? storedPassword, string? storedDept)
    {
        var trimmedUser = username?.Trim() ?? "";
        var trimmedPass = password ?? "";
        if (string.IsNullOrEmpty(trimmedUser)) return new AuthResult { Reason = "Ingrese el usuario" };
        if (string.IsNullOrEmpty(trimmedPass)) return new AuthResult { Reason = "Ingrese la contraseña" };
        if (storedPassword == null) return new AuthResult { Reason = "Usuario o contraseña incorrectos" };

        var dept = storedDept?.Trim() ?? "";

        if (!string.Equals(storedPassword.Trim(), trimmedPass, StringComparison.Ordinal))
            return new AuthResult { Reason = "Usuario o contraseña incorrectos" };

        if (!dept.Equals("sistemas", StringComparison.OrdinalIgnoreCase))
            return new AuthResult { Reason = "Acceso restringido: solo usuarios del departamento Sistemas" };

        var user = new UserModel { Usuario = trimmedUser, Departamento = dept };
        return new AuthResult { Success = true, User = user };
    }
}