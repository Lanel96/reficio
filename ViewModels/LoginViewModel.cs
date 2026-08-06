using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Reficio.Models;
using Reficio.Services;

namespace Reficio.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private Window? _window;

    [ObservableProperty] private string _usuario = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string _dbPath = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _hasError;

    public UserModel? AuthenticatedUser { get; private set; }
    public event Action<bool>? LoginCompleted;

    public void SetWindow(Window window) => _window = window;

    [RelayCommand]
    private async Task BrowseAsync()
    {
        if (_window == null) { StatusText = "Error: ventana no disponible"; HasError = true; return; }
        try
        {
            var files = await _window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Seleccionar base de datos Firebird",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("Firebird Database") { Patterns = new[] { "*.fdb", "*.FDB" } } }
            });
            if (files.Count > 0) DbPath = files[0].Path.LocalPath;
        }
        catch (Exception ex)
        {
            StatusText = $"Error al abrir explorador: {ex.Message}";
            HasError = true;
        }
    }

    [RelayCommand]
    private void Login()
    {
        HasError = false;
        StatusText = "";

        if (string.IsNullOrWhiteSpace(Usuario)) { HasError = true; StatusText = "Ingrese usuario"; return; }
        if (string.IsNullOrWhiteSpace(Password)) { HasError = true; StatusText = "Ingrese contraseña"; return; }
        if (string.IsNullOrWhiteSpace(DbPath)) { HasError = true; StatusText = "Ruta de base de datos requerida"; return; }

        try
        {
            using var db = new FirebirdDbService(DbPath, "SYSDBA", "masterkey");
            db.TestConnection();

            var user = AuthService.Validate(db, Usuario, Password);
            if (user == null) { HasError = true; StatusText = "Usuario o contraseña incorrectos"; return; }
            if (!user.EsSistema) { HasError = true; StatusText = "Solo usuarios de sistemas pueden acceder"; return; }

            AuthenticatedUser = user;
            StatusText = "Acceso concedido";
            LoginCompleted?.Invoke(true);
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusText = $"Error de conexión: {ex.Message}";
        }
    }
}
