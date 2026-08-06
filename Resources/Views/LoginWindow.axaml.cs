using Avalonia.Controls;
using Reficio.ViewModels;

namespace Reficio.Views;

public partial class LoginWindow : Window
{
    public LoginViewModel? ViewModel => DataContext as LoginViewModel;

    public LoginWindow()
    {
        InitializeComponent();
        if (DataContext is LoginViewModel vm)
            vm.SetWindow(this);
    }
}
