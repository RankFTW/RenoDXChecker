using Microsoft.UI.Xaml;
using RenoDXChecker.Services;

namespace RenoDXChecker;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        // Register crash/error reporting before anything else runs.
        // This catches AppDomain, TaskScheduler, and WinUI exceptions.
        CrashReporter.Register(this);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        CrashReporter.Log("App.OnLaunched — creating MainWindow");
        _window = new MainWindow();
        _window.Activate();
        CrashReporter.Log("MainWindow activated");
    }
}
