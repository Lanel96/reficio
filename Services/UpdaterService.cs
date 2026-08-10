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
    private const string DefaultGitHost = "github.com";
    private const string DefaultGitHubOwner = "Lanel96";
    private const string DefaultGitHubRepo = "reficio";
    private const string DefaultPackageName = "reficio";
    private static readonly string DefaultApiBaseUrl = $"https://api.github.com/repos/{DefaultGitHubOwner}/{DefaultGitHubRepo}";
    
    private static HttpClient? _httpClient;
    private static readonly object HttpClientLock = new();
    private static Timer? _checkTimer;
    
    public static string GitHost { get; set; } = DefaultGitHost;
    public static string GitHubOwner { get; set; } = DefaultGitHubOwner;
    public static string GitHubRepo { get; set; } = DefaultGitHubRepo;
    public static string PackageName { get; set; } = DefaultPackageName;
    public static string ApiBaseUrl { get; set; } = DefaultApiBaseUrl;
    
    static UpdaterService()
    {
        InitializeHttpClient();
    }
    
    private static void InitializeHttpClient()
    {
        lock (HttpClientLock)
        {
            _httpClient?.Dispose();
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Reficio/" + GetCurrentVersion());
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
        }
    }
    
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
                LogService.LogError("Error comprobando actualizaciones", ex);
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
    
    public static bool LaunchUpdater(string downloadUrl, out string error)
    {
        error = "";
        try
        {
            var execDir = AppDomain.CurrentDomain.BaseDirectory;
            var updaterName = OperatingSystem.IsWindows() ? "ReficioUpdater.exe" : "ReficioUpdater";
            var updaterPath = Path.Combine(execDir, updaterName);
            
            if (!File.Exists(updaterPath))
            {
                updaterPath = ExtractEmbeddedUpdater(updaterName);
                if (updaterPath == null)
                {
                    error = $"no se encontró el actualizador ({updaterName}). Reinstale la aplicación.";
                    LogService.LogError($"Falta actualizador y no hay recurso embebido: {updaterName}");
                    return false;
                }
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
            LogService.Log($"Actualizador lanzado: {updaterPath} url={downloadUrl}");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            LogService.LogError("Error lanzando actualizador", ex);
            return false;
        }
    }
    
    private static string? ExtractEmbeddedUpdater(string fileName)
    {
        var resourceNames = Assembly.GetExecutingAssembly().GetManifestResourceNames();
        var updaterResource = resourceNames.FirstOrDefault(r =>
            r.EndsWith("ReficioUpdater.exe", StringComparison.OrdinalIgnoreCase) ||
            r.EndsWith("ReficioUpdater", StringComparison.OrdinalIgnoreCase));
        
        if (updaterResource == null)
        {
            LogService.LogError($"No se encontró recurso embebido del actualizador. Recursos: {string.Join(", ", resourceNames)}");
            return null;
        }
        
        var tempDir = Path.Combine(Path.GetTempPath(), "reficio_updater_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempPath = Path.Combine(tempDir, fileName);
        
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(updaterResource);
        if (stream == null)
        {
            LogService.LogError($"No se pudo leer recurso embebido: {updaterResource}");
            return null;
        }
        
        using var fs = File.Create(tempPath);
        stream.CopyTo(fs);
        
        if (!OperatingSystem.IsWindows())
        {
            try { Process.Start("chmod", $"+x {tempPath}")?.WaitForExit(); } catch { }
        }
        
        LogService.Log($"Actualizador extraído: {tempPath}");
        return tempPath;
    }
    
    private static async Task<(string version, string downloadUrl)> GetLatestReleaseAsync()
    {
        var token = GetAuthToken();
        var url = $"{ApiBaseUrl}/releases/latest";
        LogService.Log($"Consulta release: url={url} tokenhash={TokenHash(token ?? "")}");
        
        var json = await GetJsonAsync(url, token);
        var release = JsonConvert.DeserializeObject<GitHubRelease>(json);
        if (release == null || string.IsNullOrEmpty(release.TagName))
            throw new Exception("No se encontró ninguna Release publicada en el repositorio GitHub");
        
        var latest = release.TagName.TrimStart('v');
        if (!Version.TryParse(latest, out _))
            throw new Exception($"La Release '{release.TagName}' no tiene una versión válida");
        
        var fileName = $"Reficio-{GetPlatformTag()}.zip";
        var asset = release.Assets?.FirstOrDefault(a => a.Name == fileName);
        if (asset == null || string.IsNullOrEmpty(asset.ApiUrl))
            throw new Exception($"No se encontró el instalador '{fileName}' en la Release v{latest}");
        
        return (latest, asset.ApiUrl);
    }
    
    private static async Task<string> GetJsonAsync(string url, string? token, int maxAttempts = 4)
    {
        HttpResponseMessage? lastResponse = null;
        
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var client = GetHttpClient();
                if (!string.IsNullOrEmpty(token))
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                else
                    client.DefaultRequestHeaders.Authorization = null;
                
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
                
                var response = await client.SendAsync(request);
                lastResponse = response;
                var code = (int)response.StatusCode;
                
                LogService.Log($"GET {code} intento {attempt}/{maxAttempts}");
                
                if (code == 200)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                
                if (code == 401 && !string.IsNullOrEmpty(token))
                {
                    LogService.Log("401: reintentando sin token (repo público)");
                    client.DefaultRequestHeaders.Authorization = null;
                    using var retryRequest = new HttpRequestMessage(HttpMethod.Get, url);
                    retryRequest.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
                    var retryResponse = await client.SendAsync(retryRequest);
                    if ((int)retryResponse.StatusCode == 200)
                        return await retryResponse.Content.ReadAsStringAsync();
                    lastResponse = retryResponse;
                }
                
                if (code == 404)
                    throw new Exception("404: no se encontró el repositorio o no hay releases publicadas. Verifica que el repo exista y tenga al menos una Release publicada.");
                
                if (code == 403)
                    throw new Exception("403: acceso denegado. Verifica límites de rate limiting de GitHub API.");
                
                if (code is 429 or >= 500 and <= 599)
                {
                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(1200 * attempt);
                        continue;
                    }
                }
                
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"Error HTTP {code}: {errorBody}");
            }
            catch (HttpRequestException ex)
            {
                LogService.LogError($"GET excepción intento {attempt}", ex);
                if (attempt < maxAttempts)
                    await Task.Delay(1200 * attempt);
                else
                    throw;
            }
        }
        
        throw new Exception($"Falló después de {maxAttempts} intentos. Último estado: {(int?)lastResponse?.StatusCode}");
    }
    
    private static HttpClient GetHttpClient()
    {
        lock (HttpClientLock)
        {
            if (_httpClient == null)
                InitializeHttpClient();
            return _httpClient!;
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
                    var userinfo = rest.Substring(0, at);
                    var colon = userinfo.IndexOf(':');
                    var token = colon >= 0 ? userinfo.Substring(colon + 1) : userinfo;
                    if (!string.IsNullOrEmpty(token)) return token;
                }
        var p = Environment.GetEnvironmentVariable("GIT_PASSWORD");
        if (!string.IsNullOrEmpty(p)) return p;
        return null;
    }
    
    private static string TokenHash(string token)
    {
        if (string.IsNullOrEmpty(token)) return "(vacío)";
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
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