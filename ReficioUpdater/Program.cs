using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;

namespace ReficioUpdater;

internal static class Program
{
    private const string GitHost = "github.com";

    private static readonly string DebugLogPath =
        Path.Combine(Path.GetTempPath(), "reficio_updater_debug.log");

    private static void DebugLog(string msg)
    {
        try { File.AppendAllText(DebugLogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
    }

    private static int Main(string[] args)
    {
        try
        {
            var (url, installDir, exeName) = ParseArgs(args);
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(installDir))
            {
                Console.WriteLine("[ERROR] Uso: ReficioUpdater --url <asset-url> --dir <install-dir> [--exe <exe>]");
                return 2;
            }

            DebugLog($"Iniciado: url={url} dir={installDir} exe={exeName}");

            // La app principal ya se está cerrando; esperar a que libere el archivo.
            Thread.Sleep(2500);

            // 1. Descargar el instalador.
            var zipPath = Download(url);
            Console.WriteLine("Descarga completada. Extrayendo...");
            DebugLog("Descarga completada");

            // 2. Extraer a una carpeta temporal.
            var extractDir = Path.Combine(Path.GetTempPath(), "reficio_update_extract_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(extractDir);
            ZipFile.ExtractToDirectory(zipPath, extractDir);

            // 3. Reemplazar los archivos de la aplicación.
            Console.WriteLine("Instalando...");
            Install(extractDir, installDir, exeName);

            // 4. Relanzar la aplicación principal.
            if (!string.IsNullOrEmpty(exeName))
            {
                var exePath = Path.Combine(installDir, exeName);
                if (File.Exists(exePath))
                {
                    Console.WriteLine("Relanzando aplicación...");
                    Process.Start(new ProcessStartInfo(exePath) { WorkingDirectory = installDir, UseShellExecute = true });
                }
            }

            Cleanup(zipPath, extractDir);
            Console.WriteLine("Actualización completada.");
            DebugLog("Actualización completada");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] {ex.Message}");
            DebugLog($"ERROR: {ex}");
            return 1;
        }
    }

    private static (string url, string installDir, string exeName) ParseArgs(string[] args)
    {
        string url = "", dir = "", exe = "";
        for (int i = 0; i < args.Length - 1; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--url": url = args[++i]; break;
                case "--dir": dir = args[++i]; break;
                case "--exe": exe = args[++i]; break;
            }
        }
        if (string.IsNullOrEmpty(exe)) exe = "Reficio.exe";
        return (url, dir, exe);
    }

    private static string Download(string url)
    {
        // Para repos públicos no se requiere token; para privados se usa si existe en ~/.git-credentials o GIT_PASSWORD
        var token = GetAuthToken();

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "ReficioUpdater/1.4.8");
        if (!string.IsNullOrEmpty(token))
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {token}");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/octet-stream");

        var zipPath = Path.Combine(Path.GetTempPath(), $"reficio_download_{Guid.NewGuid():N}.zip");
        Console.WriteLine("Descargando actualización...");

        using var response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
        var code = (int)response.StatusCode;
        DebugLog($"Descarga status: {code}");
        if (code == 404)
        {
            string body = "";
            try { body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult(); } catch { }
            DebugLog($"Descarga 404 body: {body}");
            throw new Exception(
                "404 al descargar el paquete. El asset no existe en la Release o el repo es privado " +
                "y requiere token. Si el repo es privado, configura un PAT con scope 'repo' en " +
                "~/.git-credentials o variable GIT_PASSWORD. URL: " + url);
        }
        if (code == 401)
        {
            throw new Exception(
                "401: el repositorio es privado y el token es inválido o expiró. " +
                "Configura un PAT válido con scope 'repo' en ~/.git-credentials o GIT_PASSWORD.");
        }
        response.EnsureSuccessStatusCode();

        using (var stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
        using (var fs = File.Create(zipPath))
        {
            stream.CopyTo(fs);
        }
        return zipPath;
    }

    private static void Install(string extractDir, string installDir, string exeName)
    {
        if (OperatingSystem.IsWindows())
        {
            // El zip contiene windows-x64/Reficio.exe (publicado con PublishSingleFile).
            var sourceDir = Path.Combine(extractDir, "windows-x64");
            if (!Directory.Exists(sourceDir)) sourceDir = extractDir;

            var srcExe = Directory.GetFiles(sourceDir, "*.exe").FirstOrDefault()
                         ?? Path.Combine(sourceDir, exeName);
            if (!File.Exists(srcExe))
                throw new Exception($"No se encontró el ejecutable en el paquete descargado: {srcExe}");

            var destExe = Path.Combine(installDir, exeName);
            var backup = destExe + ".old";

            try { if (File.Exists(backup)) File.Delete(backup); } catch { }
            try { if (File.Exists(destExe)) File.Move(destExe, backup); } catch { }
            File.Copy(srcExe, destExe, true);
            DebugLog($"Reemplazado {destExe}");
        }
        else if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        {
            var platform = GetPlatformTag();
            var bundleSrc = Path.Combine(extractDir, platform, "Reficio.app", "Contents");
            if (Directory.Exists(bundleSrc))
            {
                var appDir = FindAppBundle(installDir);
                if (appDir != null)
                {
                    CopyDirectory(Path.Combine(bundleSrc, "MacOS"), Path.Combine(appDir, "Contents", "MacOS"));
                    CopyDirectory(Path.Combine(bundleSrc, "Resources"), Path.Combine(appDir, "Contents", "Resources"));
                }
                else
                {
                    CopyDirectory(bundleSrc, installDir);
                }
            }
            else
            {
                CopyDirectory(Path.Combine(extractDir, platform), installDir);
            }
        }
    }

    private static string GetPlatformTag()
    {
        if (OperatingSystem.IsWindows()) return "windows-x64";
        if (OperatingSystem.IsMacOS())
        {
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64) return "macos-arm64";
            return "macos-x64";
        }
        if (OperatingSystem.IsLinux()) return "linux-x64";
        return "unknown";
    }

    private static string? FindAppBundle(string installDir)
    {
        var dir = installDir;
        for (int i = 0; i < 5; i++)
        {
            if (dir == null) break;
            if (dir.EndsWith(".app/Contents/MacOS") || dir.EndsWith(".app/Contents"))
                return Path.GetDirectoryName(dir) ?? dir;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static void CopyDirectory(string source, string dest)
    {
        if (!Directory.Exists(source)) return;
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }

    private static void Cleanup(string zipPath, string extractDir)
    {
        try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
        try { if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true); } catch { }
    }

    private static string? GetAuthToken()
    {
        var credPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".git-credentials");
        if (File.Exists(credPath))
            foreach (var line in File.ReadLines(credPath))
                if (line.Contains(GitHost) && line.Contains("://"))
                {
                    var idx = line.IndexOf("://", StringComparison.Ordinal);
                    if (idx < 0) continue;
                    var rest = line.Substring(idx + 3);
                    var at = rest.LastIndexOf('@');
                    if (at <= 0) continue;
                    var userinfo = rest.Substring(0, at);
                    var colon = userinfo.IndexOf(':');
                    var token = colon >= 0 ? userinfo.Substring(colon + 1) : userinfo;
                    if (!string.IsNullOrEmpty(token)) return token;
                }
        var p = Environment.GetEnvironmentVariable("GIT_PASSWORD");
        if (!string.IsNullOrEmpty(p)) return p;
        return null;
    }
}
