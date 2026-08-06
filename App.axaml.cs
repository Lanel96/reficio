using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var vm = new LoginViewModel();
            var loginWindow = new LoginWindow { DataContext = vm };
            vm.SetWindow(loginWindow);

            vm.LoginCompleted += authenticated =>
            {
                if (authenticated)
                {
                    var mainVm = new MainViewModel();
                    var window = new MainWindow { DataContext = mainVm };
                    window.Closed += (_, _) =>
                    {
                        mainVm.SaveConfig();
                        UpdaterService.StopAutoCheck();
                        desktop.Shutdown();
                    };
                    desktop.MainWindow = window;
                    desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    window.Show();
                }
                else
                {
                    desktop.Shutdown();
                }
                loginWindow.Close();
            };

            loginWindow.Show();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
