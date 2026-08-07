using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Reficio.Models;
using Reficio.Services;

namespace Reficio.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty] private string _host = "localhost";
    [ObservableProperty] private int _port = 3050;
    [ObservableProperty] private string _dbPath = "";
    [ObservableProperty] private string _dbUser = "SYSDBA";
    [ObservableProperty] private string _dbPassword = "";
    [ObservableProperty] private string _binDir = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isBusy;

    public SettingsViewModel(bool isSetup = false)
    {
        var config = ConnectionConfigService.Load();
        var appConfig = ConfigService.Load();
        if (config != null)
        {
            Host = config.Host;
            Port = config.Port;
            DbPath = config.DbPath;
            DbUser = config.DbUser;
            DbPassword = config.DbPassword;
        }
        if (appConfig != null) BinDir = appConfig.BinDir;
        StatusText = isSetup ? "Configure la conexión a la base de datos" : "Datos de conexión";
    }

    [RelayCommand]
    private async Task BrowseDbPathAsync()
    {
        var window = GetWindow();
        if (window == null) return;
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Seleccionar base de datos Firebird",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Firebird Database") { Patterns = new[] { "*.fdb", "*.FDB" } } }
        });
        if (files.Count > 0) DbPath = files[0].Path.LocalPath;
    }

    [RelayCommand]
    private async Task BrowseBinDirAsync()
    {
        var window = GetWindow();
        if (window == null) return;
        var dirs = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Seleccionar carpeta de herramientas Firebird",
            AllowMultiple = false
        });
        if (dirs.Count > 0) BinDir = dirs[0].Path.LocalPath;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrEmpty(DbPath)) { StatusText = "Indique la ruta de la base de datos"; return; }
        if (!DbPath.EndsWith(".fdb", StringComparison.OrdinalIgnoreCase))
        { StatusText = "La base de datos debe tener extensión .fdb"; return; }

        IsBusy = true;
        StatusText = "Probando conexión...";
        try
        {
            await Task.Run(() =>
            {
                using var db = new FirebirdDbService(Host, Port, DbPath, DbUser, DbPassword);
                db.TestConnection();
            });
        }
        catch (Exception ex)
        {
            StatusText = $"No se pudo conectar: {ex.Message}";
            IsBusy = false;
            return;
        }

        ConnectionConfigService.Save(new DbConnectionConfig
        {
            Host = Host,
            Port = Port,
            DbPath = DbPath,
            DbUser = DbUser,
            DbPassword = DbPassword
        });

        var appConfig = ConfigService.Load();
        appConfig.BinDir = BinDir;
        ConfigService.Save(appConfig);

        IsBusy = false;
        StatusText = "Conexión guardada correctamente";
        Closed?.Invoke();
    }

    public event Action? Closed;

    private static Window? GetWindow()
        => Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
}