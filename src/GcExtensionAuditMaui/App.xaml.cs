using GcExtensionAuditMaui.Services;
using GcExtensionAuditMaui.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GcExtensionAuditMaui;

public partial class App : Application
{
    // Window size constants
    private const int DefaultWindowWidth = 1400;
    private const int DefaultWindowHeight = 900;
    private const int ErrorWindowWidth = 900;
    private const int ErrorWindowHeight = 700;

    private readonly IServiceProvider _services;
    private readonly LoggingService _log;

    public App(IServiceProvider services, OutputPathService paths, LoggingService log)
    {
        InitializeComponent();

        _services = services;
        _log = log;
        log.Initialize(paths.GetDefaultLogPath());
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        try
        {
            var main = _services.GetRequiredService<DashboardPage>();
            return new Window(main)
            {
                Title = "Genesys Audits",
                Width = DefaultWindowWidth,
                Height = DefaultWindowHeight,
            };
        }
        catch (Exception ex)
        {
            _log.Log(Models.Logging.LogLevel.Error, "Startup failed while creating the main window", ex: ex);

            var fallback = new ContentPage
            {
                Title = "Startup Error",
                Content = new ScrollView
                {
                    Content = new VerticalStackLayout
                    {
                        Padding = 24,
                        Spacing = 12,
                        Children =
                        {
                            new Label { Text = "The app failed to start.", FontSize = 20, FontAttributes = FontAttributes.Bold },
                            new Label { Text = "Details:", FontAttributes = FontAttributes.Bold },
                            new Label { Text = ex.ToString(), FontFamily = "Consolas" },
                            new Label { Text = "Log file:", FontAttributes = FontAttributes.Bold },
                            new Label { Text = _log.LogPath ?? "(log path not available)", FontFamily = "Consolas" },
                        }
                    }
                }
            };

            return new Window(fallback)
            {
                Title = "Genesys Audits (Startup Error)",
                Width = ErrorWindowWidth,
                Height = ErrorWindowHeight,
            };
        }
    }
}
