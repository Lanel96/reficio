using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using Reficio.Models;

namespace Reficio.Services;

public static class UpdaterService
{
    private const string GitHost = "github.com";
    private const string GitHubOwner = "Lanel96";
    private const string GitHubRepo = "reficio";
    private const string PackageName = "reficio";
    private static readonly string ApiBaseUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}";
    // Token por defecto para que la actualización funcione sin configurar credenciales.
    // Se puede sobreescribir guardando otro token en ~/.git-credentials o en GIT_PASSWORD.
    private const string DefaultToken = "";
    private static readonly string DebugLogPath = Path.Combine(System.IO.Path.GetTempPath(), "reficio_update_debug.log");
    private static readonly object DebugLock = new();
    private static void DebugLog(string msg)
    {
        try { lock (DebugLock) System.IO.File.AppendAllText(DebugLogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
    }
    private static string TokenHash(string token)
    {
        if (string.IsNullOrEmpty(token)) return "(vacío)";
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    static UpdaterService()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("Reficio/" + GetCurrentVersion());
        Http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
    }
    private static Timer? _checkTimer;

    public static string GetCurrentVersion()
    {
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        if (ver != null && ver.ToString(3) != "0.0.0") return ver.ToString(3);
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VERSION");
        return File.Exists(path) ? File.ReadAllText(path).Trim() : "0.0.0";
    }

    public static void StartAutoCheck(Action<string> onUpdateAvailable, Action<string>? onError = null)
    {
        _checkTimer?.Dispose();
        _checkTimer = new Timer(async _ =>
        {
            try
            {
                var info = await CheckForUpdateAsync();
                if (info.Available)
                    onUpdateAvailable(info.LatestVersion);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
            }
        }, null, TimeSpan.FromSeconds(10), TimeSpan.FromHours(1));
    }

    public static void StopAutoCheck() => _checkTimer?.Dispose();

    public static async Task<UpdateInfo> CheckForUpdateAsync()
    {
        var current = GetCurrentVersion();
        var (latest, downloadUrl) = await GetLatestReleaseAsync();
        return new UpdateInfo
        {
            CurrentVersion = current,
            LatestVersion = latest,
            Available = CompareVersions(latest, current) > 0,
            DownloadUrl = downloadUrl
        };
    }

    public static async Task<bool> DownloadAndInstallUpdateAsync(string downloadUrl, Action<double, string>? onProgress = null)
    {
        var platform = GetPlatformTag();
        var zipName = $"Reficio-{platform}.zip";

        onProgress?.Invoke(0.1, "Descargando actualización...");
        var tmpDir = Path.Combine(Path.GetTempPath(), "reficio_update");
        Directory.CreateDirectory(tmpDir);
        var zipPath = Path.Combine(tmpDir, zipName);

        try
        {
            var token = GetAuthToken();
            try { DebugLog($"Descarga: url={downloadUrl} tokenhash={TokenHash(token)}"); } catch { }
            var response = await GetWithRetryAsync(downloadUrl, acceptOctetStream: true);
            DebugLog($"Descarga status: {(int)response.StatusCode}");
            response.EnsureSuccessStatusCode();
        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var fs = File.Create(zipPath);
        if (totalBytes > 0)
        {
            var buffer = new byte[8192];
            long read;
            long total = 0;
            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                await fs.WriteAsync(buffer.AsMemory(0, (int)read));
                total += read;
                onProgress?.Invoke(0.1 + 0.5 * (total / (double)totalBytes), $"Descargando... {total / 1024 / 1024:F1} MB");
            }
        }
        else
        {
            await stream.CopyToAsync(fs);
        }
        fs.Close();

        onProgress?.Invoke(0.6, "Extrayendo archivos...");
        var extractDir = Path.Combine(tmpDir, "extract");
        if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
        ZipFile.ExtractToDirectory(zipPath, extractDir);

        onProgress?.Invoke(0.8, "Instalando...");
        var execDir = AppDomain.CurrentDomain.BaseDirectory;
        var execPath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;

        if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        {
            // Para macOS .app bundle, el ejecutable está en Contents/MacOS/
            var appDir = FindAppBundle(execDir);
            if (appDir != null)
            {
                var sourceMacOS = Path.Combine(extractDir, GetPlatformTag(), "Reficio.app", "Contents", "MacOS");
                var destMacOS = Path.Combine(appDir, "Contents", "MacOS");
                if (Directory.Exists(sourceMacOS))
                    CopyDirectory(sourceMacOS, destMacOS);

                var sourceRes = Path.Combine(extractDir, GetPlatformTag(), "Reficio.app", "Contents", "Resources");
                var destRes = Path.Combine(appDir, "Contents", "Resources");
                if (Directory.Exists(sourceRes))
                    CopyDirectory(sourceRes, destRes);
            }
        }
        else
        {
            // Windows: reemplazar ejecutable
            var sourceFile = Path.Combine(extractDir, "windows-x64", "Reficio.exe");
            if (File.Exists(sourceFile))
            {
                var backup = execPath + ".old";
                try { if (File.Exists(backup)) File.Delete(backup); File.Move(execPath, backup); } catch { }
                File.Copy(sourceFile, execPath, true);
            }
        }

        onProgress?.Invoke(1.0, "Actualización instalada. Reinicie la aplicación.");
            return true;
        }
        finally
        {
            try { Directory.Delete(tmpDir, true); } catch { }
        }
    }

    private static async Task<(string version, string downloadUrl)> GetLatestReleaseAsync()
    {
        // Utiliza la Release (tag) más reciente publicada en GitHub.
        var token = GetAuthToken();
        if (string.IsNullOrEmpty(token))
            throw new Exception(
                "no hay credenciales configuradas (archivo .git-credentials). Ejecute Reficio_setup_creds.bat");

        var url = $"{ApiBaseUrl}/releases/latest";
        DebugLog($"Consulta: url={url} tokenhash={TokenHash(token)}");
        string json;
        try
        {
            using var releaseResponse = await GetWithRetryAsync(url);
            json = await releaseResponse.Content.ReadAsStringAsync();
            DebugLog($"Consulta status: {(int)releaseResponse.StatusCode}");
            releaseResponse.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            DebugLog($"Consulta status: 404");
            throw new Exception("el token de GitHub es inválido, el repo no existe, o no hay releases (404)", ex);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            DebugLog($"Consulta status: 401");
            throw new Exception("el token de GitHub es rechazado (401)", ex);
        }

        var release = JsonConvert.DeserializeObject<GitHubRelease>(json);
        if (release == null || string.IsNullOrEmpty(release.TagName))
            throw new Exception("No se encontró ninguna Release publicada en el repositorio GitHub");

        var latest = release.TagName.TrimStart('v');
        if (!System.Version.TryParse(latest, out _))
            throw new Exception($"La Release '{release.TagName}' no tiene una versión válida");

        var fileName = $"Reficio-{GetPlatformTag()}.zip";
        var asset = release.Assets?.FirstOrDefault(a => a.Name == fileName);
        if (asset == null || string.IsNullOrEmpty(asset.ApiUrl))
            throw new Exception($"No se encontró el instalador '{fileName}' en la Release v{latest}");

        // Repos privados: se debe descargar vía la API del asset con Accept: application/octet-stream.
        return (latest, asset.ApiUrl);
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

    private static string? FindAppBundle(string execDir)
    {
        var dir = execDir;
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
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }

    private static int CompareVersions(string v1, string v2)
    {
        var p1 = v1.Split('.'); var p2 = v2.Split('.');
        for (int i = 0; i < Math.Max(p1.Length, p2.Length); i++)
        {
            var n1 = i < p1.Length && int.TryParse(p1[i], out var a) ? a : 0;
            var n2 = i < p2.Length && int.TryParse(p2[i], out var b) ? b : 0;
            if (n1 > n2) return 1; if (n1 < n2) return -1;
        }
        return 0;
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
                    var userinfo = rest.Substring(0, at);   // "usuario:token" o solo "token"
                    var colon = userinfo.IndexOf(':');
                    var token = colon >= 0 ? userinfo.Substring(colon + 1) : userinfo;
                    if (!string.IsNullOrEmpty(token)) return token;
                }
        var u = Environment.GetEnvironmentVariable("GIT_USERNAME");
        var p = Environment.GetEnvironmentVariable("GIT_PASSWORD");
        if (!string.IsNullOrEmpty(u) && !string.IsNullOrEmpty(p)) return p;
        return DefaultToken;
    }

    private static void AddAuth(HttpClient client, string token)
    {
        if (!string.IsNullOrEmpty(token))
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {token}");
    }

    // Reintenta ante fallos transitorios (401/429/5xx o de red) manteniendo la URL y token válidos.
    private static async Task<HttpResponseMessage> GetWithRetryAsync(string url, bool acceptOctetStream = false, int maxAttempts = 4)
    {
        HttpResponseMessage? last = null;
        for (int i = 1; i <= maxAttempts; i++)
        {
            try
            {
                AddAuth(Http, GetAuthToken());
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (acceptOctetStream)
                    request.Headers.TryAddWithoutValidation("Accept", "application/octet-stream");
                var response = await Http.SendAsync(request);
                var code = (int)response.StatusCode;
                if (code == 200) return response;
                last = response;
                DebugLog($"GET {(int)response.StatusCode} intento {i}/{maxAttempts}");
                if (code is 401 or 429 or >= 500 && code <= 599)
                {
                    if (i < maxAttempts)
                    {
                        response.Dispose();
                        await Task.Delay(1200 * i);
                        continue;
                    }
                }
                return response;
            }
            catch (Exception ex)
            {
                DebugLog($"GET excepción intento {i}: {ex.Message}");
                await Task.Delay(1200 * i);
            }
        }
        // Devuelve la última respuesta (o una 503 si todo falló con excepción de red).
        return last ?? new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
    }

    private class GitHubRelease
    {
        [JsonProperty("tag_name")] public string TagName { get; set; } = "";
        [JsonProperty("assets")] public List<GitHubAsset>? Assets { get; set; }
    }

    private class GitHubAsset
    {
        [JsonProperty("name")] public string Name { get; set; } = "";
        [JsonProperty("url")] public string ApiUrl { get; set; } = "";
    }
}
