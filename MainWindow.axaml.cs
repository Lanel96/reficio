using Avalonia;
using Avalonia.Controls;
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
}