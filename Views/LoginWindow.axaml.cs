using Avalonia.Controls;
using Reficio.Models;
using Reficio.Services;
using Reficio.ViewModels;

namespace Reficio.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _vm;
    private bool _setupOpen;

    public event Action<UserModel, FirebirdDbService>? LoginSucceeded;

    public LoginWindow()
    {
        InitializeComponent();
        _vm = new LoginViewModel();
        _vm.LoginSucceeded += (user, db) => LoginSucceeded?.Invoke(user, db);
        _vm.RequiresSetup += OpenSetup;
        DataContext = _vm;

        Loaded += (_, _) =>
        {
            if (!ConnectionConfigService.Exists())
                OpenSetup();
        };
    }

    private void OpenSetup()
    {
        if (_setupOpen) return;
        _setupOpen = true;
        var setup = new SettingsWindow();
        setup.Closed += (_, _) =>
        {
            _setupOpen = false;
            _vm.Refresh();
            if (!ConnectionConfigService.Exists()) Close();
        };
        setup.ShowDialog(this);
    }
}