using Reficio.Models;

namespace Reficio.Services;

public static class AuthService
{
    public static UserModel? Validate(FirebirdDbService db, string usuario, string password)
    {
        var result = db.Query(
            "SELECT USUA, DEPA FROM MUSUA WHERE USUA = @p0 AND PASE = @p1",
            usuario, password);

        if (result.Rows.Count == 0) return null;

        var row = result.Rows[0];
        return new UserModel
        {
            Usuario = row["USUA"]?.ToString() ?? "",
            Departamento = row["DEPA"]?.ToString() ?? ""
        };
    }
}
