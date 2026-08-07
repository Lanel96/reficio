using System.Diagnostics;
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

    // Lanza el subprograma ReficioUpdater.exe para descargar e instalar la actualización.
    // Así la app principal nunca se reemplaza a sí misma en caliente (evita bloqueos y
    // fallos que comprometan el programa).
    public static bool LaunchUpdater(string downloadUrl, out string error)
    {
        error = "";
        try
        {
            var execDir = AppDomain.CurrentDomain.BaseDirectory;
            var updaterPath = Path.Combine(execDir, OperatingSystem.IsWindows() ? "ReficioUpdater.exe" : "ReficioUpdater");

            if (!File.Exists(updaterPath))
            {
                error = $"no se encontró el actualizador ({Path.GetFileName(updaterPath)}). Reinstale la aplicación.";
                DebugLog($"Falta actualizador: {updaterPath}");
                return false;
            }

            var exeName = OperatingSystem.IsWindows() ? "Reficio.exe"
                : Path.GetFileName(Environment.ProcessPath ?? "Reficio");

            var psi = new ProcessStartInfo
            {
                FileName = updaterPath,
                WorkingDirectory = execDir,
                UseShellExecute = false
            };
            psi.ArgumentList.Add("--url");
            psi.ArgumentList.Add(downloadUrl);
            psi.ArgumentList.Add("--dir");
            psi.ArgumentList.Add(execDir);
            psi.ArgumentList.Add("--exe");
            psi.ArgumentList.Add(exeName);

            Process.Start(psi);
            DebugLog($"Actualizador lanzado: {updaterPath} url={downloadUrl}");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            DebugLog($"Error lanzando actualizador: {ex.Message}");
            return false;
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
