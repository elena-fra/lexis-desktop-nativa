using Avalonia.Controls;

namespace Lexis.Desktop.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowState = WindowState.Normal;
        Opened += (_, _) =>
        {
            Activate();
            Topmost = true;
            Topmost = false;
        };
    }
}
