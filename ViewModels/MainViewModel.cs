using System.Collections.ObjectModel;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Reficio.Models;
using Reficio.Services;
using Reficio.Views;

namespace Reficio.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private FirebirdDbService? _db;
    private CorrectionModule? _facturaModule;
    private CorrectionModule? _pacienteModule;
    private AppConfig _config;
    private readonly StringBuilder _log = new();

    [ObservableProperty] private string _dbPath = "";
    [ObservableProperty] private string _user = "SYSDBA";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string _binDir = "";
    [ObservableProperty] private string _statusText = "Listo";
    [ObservableProperty] private string _logText = "";
    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _versionText = "";
    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private bool _updateAvailable;
    [ObservableProperty] private string _updateVersion = "";

    [ObservableProperty] private string _facturaCodi = "";
    [ObservableProperty] private string _facturaStatus = "Conecte a una BD";
    [ObservableProperty] private string _facturaCount = "0 registros";
    [ObservableProperty] private int _facturaSelectedIndex = -1;
    public ObservableCollection<Dictionary<string, object?>> FacturaRecords { get; } = new();

    [ObservableProperty] private string _pacienteCodi = "";
    [ObservableProperty] private string _pacienteNomb = "";
    [ObservableProperty] private string _pacienteStatus = "Conecte a una BD";
    [ObservableProperty] private string _pacienteCount = "0 registros";
    [ObservableProperty] private int _pacienteSelectedIndex = -1;
    public ObservableCollection<Dictionary<string, object?>> PacienteRecords { get; } = new();

    private Window? GetWindow()
        => Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

    public MainViewModel()
    {
        _config = ConfigService.Load();
        DbPath = _config.LastDbPath;
        User = _config.User;
        Password = "";
        BinDir = _config.BinDir;
        var ver = UpdaterService.GetCurrentVersion();
        VersionText = $"v{ver}";
        StatusText = $"Listo (v{ver})";

        UpdaterService.StartAutoCheck(newVersion =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                UpdateAvailable = true;
                UpdateVersion = newVersion;
                StatusText = $"Nueva versión disponible: v{newVersion}";
            });
        });
    }

    [RelayCommand]
    private async Task BrowseAsync()
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
    private void Connect()
    {
        var window = GetWindow();
        if (string.IsNullOrEmpty(DbPath)) { ShowError(window, "Seleccione una base de datos"); return; }
        if (!DbPath.EndsWith(".fdb", StringComparison.OrdinalIgnoreCase)) { ShowError(window, "Extensión .fdb requerida"); return; }
        try
        {
            StatusText = "Conectando...";
            _db?.Dispose();
            _db = new FirebirdDbService(DbPath, User, Password);
            _db.TestConnection();
            _facturaModule = new CorrectionModule(_db, "DINGR"); _facturaModule.LoadColumns();
            FacturaStatus = "DINGR"; FacturaCount = $"{_facturaModule.GetRecordCount()} registros";
            _pacienteModule = new CorrectionModule(_db, "MPACI"); _pacienteModule.LoadColumns();
            PacienteStatus = "MPACI"; PacienteCount = $"{_pacienteModule.GetRecordCount()} registros";
            StatusText = "Conectado"; AppendLog("Conexión exitosa");
        }
        catch (Exception ex) { StatusText = "Error de conexión"; ShowError(window, $"Error: {ex.Message}"); }
    }

    [RelayCommand] private async Task DiagnosticarAsync()
    {
        if (!ValidateDb()) return;
        if (!await ConfirmAsync("¿Diagnosticar la base de datos?")) return;
        SetRunning(true);
        Task.Run(() => { try { var r = FirebirdTools.Diagnosticar(BinDir, DbPath, User, Password, UpdateProgress); AppendLogResult(r); } finally { SetRunning(false); } });
    }

    [RelayCommand] private async Task RepararLigeroAsync()
    {
        if (!ValidateDb()) return;
        if (!await ConfirmAsync("¿Ejecutar reparación ligera?")) return;
        SetRunning(true);
        Task.Run(() => { try { var r = FirebirdTools.RepararLigero(BinDir, DbPath, User, Password, UpdateProgress); AppendLogResult(r); } finally { SetRunning(false); } });
    }

    [RelayCommand] private async Task RepararProfundoAsync()
    {
        if (!ValidateDb()) return;
        if (!await ConfirmAsync("¿Ejecutar reparación profunda? Se creará un backup y la BD original será renombrada.")) return;
        SetRunning(true);
        Task.Run(() =>
        {
            try
            {
                var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupPath = $"{DbPath}.{ts}.fbk";
                var ext = Path.GetExtension(DbPath);
                var danadaPath = DbPath[..^ext.Length] + "_dañada" + ext;
                AppendLog($"Original se renombrará a: {danadaPath}");
                var r = FirebirdTools.RepararProfundo(BinDir, DbPath, User, Password, backupPath, DbPath, UpdateProgress);
                if (r.Success) { try { File.Move(DbPath, danadaPath, true); AppendLog($"Renombrada a: {danadaPath}"); } catch (Exception ex) { r.Error = $"No se pudo renombrar: {ex.Message}"; r.Success = false; } }
                AppendLogResult(r);
            }
            finally { SetRunning(false); }
        });
    }

    [RelayCommand] private async Task SoloBackupAsync()
    {
        if (!ValidateDb()) return;
        if (!await ConfirmAsync("¿Crear backup de la base de datos?")) return;
        SetRunning(true);
        Task.Run(() => { try { var r = FirebirdTools.SoloBackup(BinDir, DbPath, User, Password, $"{DbPath}.{DateTime.Now:yyyyMMdd_HHmmss}.fbk", UpdateProgress); AppendLogResult(r); } finally { SetRunning(false); } });
    }

    [RelayCommand] private async Task VerificarAsync()
    {
        if (!ValidateDb()) return;
        if (!await ConfirmAsync("¿Verificar integridad de la base de datos?")) return;
        SetRunning(true);
        Task.Run(() => { try { var r = FirebirdTools.VerificarIntegridad(BinDir, DbPath, User, Password, UpdateProgress); AppendLogResult(r); } finally { SetRunning(false); } });
    }

    [RelayCommand] private async Task SweepAsync()
    {
        if (!ValidateDb()) return;
        if (!await ConfirmAsync("¿Ejecutar limpieza (sweep) de la base de datos?")) return;
        SetRunning(true);
        Task.Run(() => { try { var r = FirebirdTools.Sweep(BinDir, DbPath, User, Password, UpdateProgress); AppendLogResult(r); } finally { SetRunning(false); } });
    }

    [RelayCommand] private async Task NBackupAsync()
    {
        if (!ValidateDb()) return;
        if (!await ConfirmAsync("¿Crear backup NBackup de la base de datos?")) return;
        SetRunning(true);
        Task.Run(() => {
            try
            {
                var backupPath = $"{DbPath}.{DateTime.Now:yyyyMMdd_HHmmss}.nbk";
                AppendLog($"NBackup a: {backupPath}");
                var r = FirebirdTools.NBackup(BinDir, DbPath, User, Password, backupPath, 0, UpdateProgress);
                AppendLogResult(r);
            }
            finally { SetRunning(false); }
        });
    }

    [RelayCommand] private async Task UpgradeODSAsync()
    {
        if (!ValidateDb()) return;
        if (!await ConfirmAsync("¿Actualizar ODS de la base de datos? Esta operación es irreversible.")) return;
        SetRunning(true);
        Task.Run(() => { try { var r = FirebirdTools.UpgradeODS(BinDir, DbPath, User, Password, UpdateProgress); AppendLogResult(r); } finally { SetRunning(false); } });
    }

    [RelayCommand] private void SearchFactura() => DoSearchFactura();

    [RelayCommand] private void ClearFactura() { FacturaCodi = ""; FacturaRecords.Clear(); FacturaSelectedIndex = -1; FacturaCount = "0 registros"; FacturaStatus = "Conecte a una BD"; }

    [RelayCommand] private async Task EditFacturaAsync()
    {
        if (FacturaRecords.Count == 0 || FacturaSelectedIndex < 0 || FacturaSelectedIndex >= FacturaRecords.Count) { FacturaStatus = "Seleccione un registro"; return; }
        var record = FacturaRecords[FacturaSelectedIndex];
        if (!record.TryGetValue("CODI", out var codiValue) || codiValue == null) { FacturaStatus = "Registro sin CODI válido"; return; }
        var fields = new[] { "CODI", "NOMBRECI", "USOCFDI", "REGIFISC" };
        var dialog = new EditDialog(record, fields, "Editar Factura");
        var window = GetWindow();
        if (window != null && await dialog.ShowDialog<bool>(window) && _facturaModule != null)
        {
            try { _facturaModule.UpdateRecord("CODI", codiValue, dialog.UpdatedValues); DoSearchFactura(); FacturaStatus = "Registro actualizado"; }
            catch (Exception ex) { ShowError(window, $"Error: {ex.Message}"); }
        }
    }

    [RelayCommand] private void SearchPaciente() => DoSearchPaciente();

    [RelayCommand] private void ClearPaciente() { PacienteCodi = ""; PacienteNomb = ""; PacienteRecords.Clear(); PacienteSelectedIndex = -1; PacienteCount = "0 registros"; PacienteStatus = "Conecte a una BD"; }

    [RelayCommand] private async Task EditPacienteAsync()
    {
        if (PacienteRecords.Count == 0 || PacienteSelectedIndex < 0 || PacienteSelectedIndex >= PacienteRecords.Count) { PacienteStatus = "Seleccione un registro"; return; }
        var record = PacienteRecords[PacienteSelectedIndex];
        if (!record.TryGetValue("CODI", out var codiValue) || codiValue == null) { PacienteStatus = "Registro sin CODI válido"; return; }
        var fields = new[] { "NOMB", "PATE", "MATE", "NOMBPACI", "FECHNACI" };
        var dialog = new EditDialog(record, fields, "Editar Paciente");
        var window = GetWindow();
        if (window != null && await dialog.ShowDialog<bool>(window) && _pacienteModule != null)
        {
            try
            {
                var updates = new Dictionary<string, object?>(dialog.UpdatedValues);
                if (updates.ContainsKey("FECHNACI") && DateTime.TryParse(updates["FECHNACI"]?.ToString(), out var d))
                    updates["FECHNACI"] = d.ToString("yyyy-MM-dd");
                _pacienteModule.UpdateRecord("CODI", codiValue, updates);
                PacienteStatus = "Registro actualizado";
            }
            catch (Exception ex) { ShowError(window, $"Error: {ex.Message}"); }
        }
    }

    [RelayCommand] private async Task CheckForUpdateAsync()
    {
        try
        {
            StatusText = "Verificando actualizaciones...";
            var info = await UpdaterService.CheckForUpdateAsync();
            if (!info.Available) { StatusText = $"Última versión (v{info.CurrentVersion})"; return; }

            var window = GetWindow();
            var confirm = new Window
            {
                Title = "Actualización disponible",
                Width = 420, Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };
            var btnYes = new Button
            {
                Content = "Actualizar ahora",
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                Padding = new Thickness(20, 8),
                Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2563EB")),
                Foreground = Avalonia.Media.Brushes.White,
                CornerRadius = new Avalonia.CornerRadius(6)
            };
            var btnNo = new Button
            {
                Content = "Más tarde",
                Padding = new Thickness(20, 8),
                CornerRadius = new Avalonia.CornerRadius(6)
            };
            btnYes.Click += async (_, _) =>
            {
                confirm.Close();
                try
                {
                    await UpdaterService.DownloadAndInstallUpdateAsync(info.DownloadUrl, (p, m) =>
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusText = m);
                    });
                    StatusText = "Actualización instalada. Reinicie la app.";
                    UpdateAvailable = false;
                }
                catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
            };
            btnNo.Click += (_, _) => confirm.Close();
            confirm.Content = new StackPanel
            {
                Margin = new Thickness(24, 20),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"Versión {info.LatestVersion} disponible (actual: {info.CurrentVersion})",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        FontSize = 14,
                        Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#374151"))
                    },
                    new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Children = { btnYes, btnNo } }
                }
            };
            if (window != null) await confirm.ShowDialog(window);
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
    }

    private async Task<bool> ConfirmAsync(string message)
    {
        var window = GetWindow();
        if (window == null) return false;

        var tcs = new TaskCompletionSource<bool>();
        var dialog = new Window
        {
            Title = "Confirmar",
            Width = 420, Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };
        var btnYes = new Button
        {
            Content = "Aceptar",
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Padding = new Thickness(20, 8),
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#DC2626")),
            Foreground = Avalonia.Media.Brushes.White,
            CornerRadius = new Avalonia.CornerRadius(6)
        };
        var btnNo = new Button
        {
            Content = "Cancelar",
            Padding = new Thickness(20, 8),
            CornerRadius = new Avalonia.CornerRadius(6)
        };
        btnYes.Click += (_, _) => { tcs.TrySetResult(true); dialog.Close(); };
        btnNo.Click += (_, _) => { tcs.TrySetResult(false); dialog.Close(); };
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(24, 20),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    FontSize = 14,
                    Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#374151"))
                },
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 10,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Children = { btnYes, btnNo }
                }
            }
        };
        await dialog.ShowDialog(window);
        return await tcs.Task;
    }

    private void DoSearchFactura()
    {
        if (_facturaModule == null || string.IsNullOrEmpty(FacturaCodi)) return;
        try
        {
            var r = _facturaModule.SearchExact("CODI", FacturaCodi);
            FacturaRecords.Clear();
            foreach (var rec in r.Records) FacturaRecords.Add(rec);
            FacturaSelectedIndex = -1;
            FacturaCount = $"{r.Count} resultado{(r.Count != 1 ? "s" : "")}";
            FacturaStatus = r.Count > 0 ? "Seleccione un registro para editarlo" : "Sin resultados";
        }
        catch (Exception ex) { ShowError(GetWindow(), $"Error: {ex.Message}"); }
    }

    private void DoSearchPaciente()
    {
        if (_pacienteModule == null) return;
        try
        {
            if (!string.IsNullOrEmpty(PacienteCodi))
            {
                var r = _pacienteModule.SearchExact("CODI", PacienteCodi);
                PacienteRecords.Clear();
                foreach (var rec in r.Records) PacienteRecords.Add(rec);
            }
            else if (!string.IsNullOrEmpty(PacienteNomb))
            {
                var r = _pacienteModule.SearchByMultiple(new Dictionary<string, object?> { ["NOMB"] = PacienteNomb });
                PacienteRecords.Clear();
                foreach (var rec in r.Records) PacienteRecords.Add(rec);
            }
            else return;
            PacienteSelectedIndex = -1;
            PacienteCount = $"{PacienteRecords.Count} resultado{(PacienteRecords.Count != 1 ? "s" : "")}";
            PacienteStatus = PacienteRecords.Count > 0 ? "Seleccione un registro para editarlo" : "Sin resultados";
        }
        catch (Exception ex) { ShowError(GetWindow(), $"Error: {ex.Message}"); }
    }

    private void SetRunning(bool running) { IsRunning = running; }
    private void UpdateProgress(double p, string m) { ProgressValue = p; StatusText = m; }
    private bool ValidateDb() { if (string.IsNullOrEmpty(DbPath)) { ShowError(GetWindow(), "Seleccione una BD"); return false; } return true; }
    private void ShowError(Window? w, string msg)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            if (w == null) return;
            var dialog = new Window
            {
                Title = "Reficio",
                Width = 420, Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };
            var btn = new Button
            {
                Content = "Aceptar",
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                Padding = new Thickness(20, 8),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2563EB")),
                Foreground = Avalonia.Media.Brushes.White,
                CornerRadius = new Avalonia.CornerRadius(6)
            };
            btn.Click += (_, _) => dialog.Close();
            var panel = new StackPanel
            {
                Margin = new Thickness(24, 20),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = msg,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        FontSize = 14,
                        Foreground = new Avalonia.Media.SolidColorBrush(
                            Avalonia.Media.Color.Parse("#374151"))
                    },
                    btn
                }
            };
            dialog.Content = panel;
            await dialog.ShowDialog(w);
        });
    }
    private void AppendLog(string msg) { _log.AppendLine($"[{DateTime.Now:HH:mm:ss}] {msg}"); LogText = _log.ToString(); }
    private void AppendLogResult(RepairResult r)
    {
        AppendLog($"{(r.Success ? "✓ OK" : "✗ FAIL")} {r.Step}: {(r.HasError ? r.Error : "Completado")}");
        if (!string.IsNullOrEmpty(r.Output))
            foreach (var line in r.Output.Split('\n').Take(30))
                if (!string.IsNullOrWhiteSpace(line)) AppendLog($"  {line.Trim()}");
    }

    public void SaveConfig() { _config.LastDbPath = DbPath; _config.User = User; _config.BinDir = BinDir; ConfigService.Save(_config); }
}
