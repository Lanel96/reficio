using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Reficio.Models;
using Reficio.Services;

namespace Reficio.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private DbConnectionConfig? _config;
    private FirebirdDbService? _db;

    [ObservableProperty] private string _username = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _hasConfig;
    [ObservableProperty] private bool _isBusy;

    public event Action<UserModel, FirebirdDbService>? LoginSucceeded;
    public event Action? RequiresSetup;

    public LoginViewModel()
    {
        _config = ConnectionConfigService.Load();
        HasConfig = _config != null && !string.IsNullOrEmpty(_config.DbPath);
        StatusText = HasConfig ? "Ingrese sus credenciales" : "Primero configure la conexión a la base de datos";
    }

    public void Refresh()
    {
        var config = ConnectionConfigService.Load();
        _config = config;
        HasConfig = config != null && !string.IsNullOrEmpty(config.DbPath);
        StatusText = HasConfig ? "Ingrese sus credenciales" : "Primero configure la conexión a la base de datos";
    }

    [RelayCommand]
    private void DoLogin()
    {
        if (IsBusy) return;
        if (!HasConfig || _config == null) { RequiresSetup?.Invoke(); return; }

        try
        {
            IsBusy = true;
            StatusText = "Conectando y validando...";
            _db?.Dispose();
            _db = new FirebirdDbService(_config.Host, _config.Port, _config.DbPath, _config.DbUser, _config.DbPassword);
            _db.TestConnection();

            var result = AuthService.Authenticate(_db, Username, Password);
            if (!result.Success)
            {
                StatusText = result.Reason;
                Password = "";
                _db.Dispose(); _db = null;
                return;
            }

            // Migración de hash: si la contraseña era texto plano, actualizar a BCrypt
            if (TryMigratePasswordHash(_db, Username, Password))
            {
                StatusText = "Contraseña actualizada a hash seguro";
            }

            StatusText = $"Bienvenido, {Username}";
            var user = result.User!;
            var db = _db;
            _db = null;
            LoginSucceeded?.Invoke(user, db);
        }
        catch (Exception ex)
        {
            StatusText = $"Error de conexión: {ex.Message}";
            _db?.Dispose(); _db = null;
            Password = "";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryMigratePasswordHash(FirebirdDbService db, string username, string plainPassword)
    {
        try
        {
            var result = db.Query("SELECT PASE FROM MUSUA WHERE TRIM(USUA) = @p0", username);
            if (result.Rows.Count == 0) return false;
            
            var storedHash = result.Rows[0].TryGetValue("PASE", out var p) ? p?.ToString()?.Trim() : "";
            if (string.IsNullOrEmpty(storedHash)) return false;
            
            // Si ya es hash BCrypt, no hacer nada
            if (storedHash.StartsWith("$2a$") || storedHash.StartsWith("$2b$") || storedHash.StartsWith("$2y$"))
            {
                // Verificar si necesita rehash (work factor bajo)
                if (AuthService.NeedsRehash(storedHash))
                {
                    var newHash = AuthService.HashPassword(plainPassword);
                    db.Execute("UPDATE MUSUA SET PASE = @p0 WHERE TRIM(USUA) = @p1", newHash, username);
                }
                return false;
            }
            
            // Es texto plano -> migrar a BCrypt
            var hash = AuthService.HashPassword(plainPassword);
            db.Execute("UPDATE MUSUA SET PASE = @p0 WHERE TRIM(USUA) = @p1", hash, username);
            return true;
        }
        catch
        {
            // No interrumpir el login si falla la migración
            return false;
        }
    }

    [RelayCommand]
    private void OpenSetup() => RequiresSetup?.Invoke();
}