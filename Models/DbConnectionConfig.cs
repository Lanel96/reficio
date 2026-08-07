namespace Reficio.Models;

public class DbConnectionConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3050;
    public string DbPath { get; set; } = "";
    public string DbUser { get; set; } = "SYSDBA";
    public string DbPassword { get; set; } = "";
}