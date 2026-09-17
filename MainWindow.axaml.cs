using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
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
    }

    private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (sender is not TextBox) return;
        if (DataContext is not MainViewModel vm) return;

        if (vm.SelectedTabIndex == 1)
            vm.SearchFacturaCommand.Execute(null);
        else if (vm.SelectedTabIndex == 2)
            vm.SearchPacienteCommand.Execute(null);
    }
}