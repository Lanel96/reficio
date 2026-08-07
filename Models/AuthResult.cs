namespace Reficio.Models;

public class AuthResult
{
    public bool Success { get; set; }
    public string Reason { get; set; } = "";
    public UserModel? User { get; set; }
}