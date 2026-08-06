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
        base.OnFrameworkInitializationCompleted();
    }
}
