namespace Reficio.Models;

public class UpdateInfo
{
    public string CurrentVersion { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
    public bool Available { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
}
