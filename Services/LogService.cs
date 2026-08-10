using System.IO;
using System.Text;

namespace Reficio.Services;

public static class LogService
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Reficio", "Logs");
    private static readonly string LogPath = Path.Combine(LogDir, $"reficio_{DateTime.Now:yyyyMMdd}.log");
    private static readonly object Lock = new();
    private static readonly StringBuilder Buffer = new();
    private static Timer? _flushTimer;
    
    static LogService()
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            _flushTimer = new Timer(_ => Flush(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            Log("=== Reficio iniciado ===");
        }
        catch { }
    }
    
    public static void Log(string message, LogLevel level = LogLevel.Info)
    {
        var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
        lock (Lock)
        {
            Buffer.AppendLine(entry);
        }
        // También escribir a debug output en desarrollo
        System.Diagnostics.Debug.WriteLine(entry);
    }
    
    public static void LogError(string message, Exception? ex = null)
    {
        var fullMessage = ex != null ? $"{message}: {ex}" : message;
        Log(fullMessage, LogLevel.Error);
    }
    
    public static void LogWarning(string message)
        => Log(message, LogLevel.Warning);
    
    public static void Flush()
    {
        string toWrite;
        lock (Lock)
        {
            if (Buffer.Length == 0) return;
            toWrite = Buffer.ToString();
            Buffer.Clear();
        }
        try
        {
            File.AppendAllText(LogPath, toWrite);
        }
        catch { }
    }
    
    public static string GetLogPath() => LogPath;
}

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}