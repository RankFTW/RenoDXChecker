using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class App : Application
{
    private Window? _window;

    /// <summary>
    /// The application-wide DI service provider.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        // Register crash/error reporting before anything else runs.
        // This catches AppDomain, TaskScheduler, and WinUI exceptions.
        CrashReporter.Register(this);

        // Configure DI container
        var services = new ServiceCollection();

        // Shared HttpClient — singleton with UserAgent header
        services.AddSingleton<HttpClient>(sp =>
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "RenoDXCommander/2.0");
            client.Timeout = TimeSpan.FromSeconds(30);
            return client;
        });

        // Services — all singletons
        services.AddSingleton<IModInstallService, ModInstallService>();
        services.AddSingleton<IAuxInstallService, AuxInstallService>();
        services.AddSingleton<IWikiService, WikiService>();
        services.AddSingleton<IManifestService, ManifestService>();
        services.AddSingleton<IGameLibraryService, GameLibraryService>();
        services.AddSingleton<IReShadeUpdateService, ReShadeUpdateService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<ILumaService, LumaService>();
        services.AddSingleton<IShaderPackService, ShaderPackService>();
        services.AddSingleton<ILiliumShaderService, LiliumShaderService>();
        services.AddSingleton<IGameDetectionService, GameDetectionService>();
        services.AddSingleton<IPeHeaderService, PeHeaderService>();

        // ViewModels
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<FilterViewModel>();

        // Extracted services
        services.AddSingleton<UpdateOrchestrationService>();
        services.AddSingleton<DllOverrideService>();
        services.AddSingleton<GameNameService>();
        services.AddSingleton<GameInitializationService>();

        services.AddSingleton<MainViewModel>();

        // Window — transient so each request creates a new instance
        services.AddTransient<MainWindow>();

        Services = services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        CrashReporter.Log("App.OnLaunched — creating MainWindow");
        _window = Services.GetRequiredService<MainWindow>();
        _window.Activate();
        CrashReporter.Log("MainWindow activated");
    }
}
