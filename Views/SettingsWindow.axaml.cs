using Avalonia.Controls;
using Reficio.ViewModels;

namespace Reficio.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;

    public SettingsWindow(bool isSetup = false)
    {
        InitializeComponent();
        _vm = new SettingsViewModel(isSetup);
        Title = isSetup ? "Configuración inicial de conexión" : "Configuración de conexión";
        _vm.Closed += () =>
        {
            if (!string.IsNullOrEmpty(_vm.StatusText) && _vm.StatusText.Contains("guardada"))
                Close();
        };
        DataContext = _vm;
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Close();
}