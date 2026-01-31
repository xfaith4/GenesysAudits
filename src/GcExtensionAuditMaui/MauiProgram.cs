using GcExtensionAuditMaui.Models.Observability;
using GcExtensionAuditMaui.Services;
using GcExtensionAuditMaui.ViewModels;
using GcExtensionAuditMaui.Views;
using Microsoft.Extensions.Logging;

namespace GcExtensionAuditMaui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<OutputPathService>();
        builder.Services.AddSingleton<LoggingService>();
        builder.Services.AddSingleton<ApiStats>();
        builder.Services.AddSingleton<PlatformOpenService>();
        builder.Services.AddSingleton<DialogService>();
        builder.Services.AddSingleton<ExportService>();
        builder.Services.AddSingleton<ContextStore>();
        builder.Services.AddSingleton<FixupPlannerService>();

        builder.Services.AddHttpClient<GenesysCloudApiClient>(client =>
        {
            // We enforce timeouts per request inside GenesysCloudApiClient.
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        builder.Services.AddSingleton<AuditService>();

        builder.Services.AddSingleton<DashboardViewModel>();
        builder.Services.AddSingleton<DashboardPage>();

        return builder.Build();
    }
}
