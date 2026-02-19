using Microsoft.UI.Xaml;

namespace RenoDXChecker;

public sealed partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        Title = "About — RenoDX Mod Manager";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(660, 720));
    }
}
