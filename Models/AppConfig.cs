namespace Reficio.Models;

public class AppConfig
{
    public string LastDbPath { get; set; } = string.Empty;
    public string User { get; set; } = "SYSDBA";
    public string Password { get; set; } = "masterkey";
    public string BinDir { get; set; } = string.Empty;
    public int WindowX { get; set; }
    public int WindowY { get; set; }
}
