namespace Reficio.Models;

public class UserModel
{
    public string Usuario { get; set; } = "";
    public string Departamento { get; set; } = "";
    public bool EsSistema => Departamento.Trim().Equals("sistemas", StringComparison.OrdinalIgnoreCase);
}