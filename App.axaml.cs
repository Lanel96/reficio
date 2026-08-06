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
            ShowLoginAndMainWindow(desktop);
        }
        base.OnFrameworkInitializationCompleted();
    }

    private async void ShowLoginAndMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var owner = new Window { Title = "Reficio", Width = 0, Height = 0, ShowInTaskbar = false, WindowState = WindowState.Minimized };
        owner.Show();

        var loginWindow = new LoginWindow();
        await loginWindow.ShowDialog<bool>(owner);
        owner.Close();

        if (loginWindow.ViewModel?.AuthenticatedUser != null)
        {
            var vm = new MainViewModel();
            var window = new MainWindow { DataContext = vm };
            window.Closed += (_, _) =>
            {
                vm.SaveConfig();
                UpdaterService.StopAutoCheck();
            };
            desktop.MainWindow = window;
            window.Show();
        }
        else
        {
            desktop.Shutdown();
        }
    }
}
