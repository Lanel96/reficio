using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Reficio.ViewModels;

namespace Reficio.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        try
        {
            Icon = new WindowIcon("avares://Reficio/Resources/Reficio.icns");
        }
        catch { }
        Closed += (_, _) =>
        {
            if (DataContext is MainViewModel vm) vm.SaveConfig();
        };
    }

    public static IStorageProvider? GetStorageProvider(Window? window)
        => window?.StorageProvider;
}
