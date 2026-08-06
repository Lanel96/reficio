namespace Reficio.Models;

public class RepairResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string Step { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public bool HasError => !string.IsNullOrEmpty(Error);
}

public delegate void ProgressCallback(double progress, string message);
