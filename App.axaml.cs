using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Reficio.Models;
using Reficio.Services;
using Reficio.ViewModels;
using Reficio.Views;

namespace Reficio;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
#if NO_LOGIN
            StartWithoutLogin(desktop);
#else
            var loginWindow = new LoginWindow();
            loginWindow.LoginSucceeded += OnLoginSucceeded;
            desktop.MainWindow = loginWindow;
            loginWindow.Show();
#endif
        }
        base.OnFrameworkInitializationCompleted();
    }

#if NO_LOGIN
    private void StartWithoutLogin(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var config = ConnectionConfigService.Load() ?? new DbConnectionConfig();
        FirebirdDbService? db = null;
        if (!string.IsNullOrEmpty(config.DbPath))
        {
            try
            {
                db = new FirebirdDbService(config.Host, config.Port, config.DbPath, config.DbUser, config.DbPassword);
                db.TestConnection();
            }
            catch { db = null; }
        }

        var user = new UserModel { Usuario = "desarrollo", Departamento = "Sistemas" };
        var vm = new MainViewModel(config, user, db);
        var window = new MainWindow { DataContext = vm };
        window.Closed += (_, _) =>
        {
            vm.SaveConfig();
            UpdaterService.StopAutoCheck();
        };
        desktop.MainWindow = window;
        window.Show();

        if (!ConnectionConfigService.Exists())
        {
            var setup = new SettingsWindow(true);
            setup.ShowDialog(window);
            vm.ApplyConnectionConfig();
        }
    }
#endif

    private void OnLoginSucceeded(UserModel user, FirebirdDbService db)
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
        if (desktop.MainWindow is not LoginWindow loginWindow) return;

        var config = ConnectionConfigService.Load() ?? new DbConnectionConfig();
        var vm = new MainViewModel(config, user, db);
        var window = new MainWindow { DataContext = vm };
        window.Closed += (_, _) =>
        {
            vm.SaveConfig();
            UpdaterService.StopAutoCheck();
        };
        desktop.MainWindow = window;
        window.Show();
        loginWindow.Close();
    }
}