using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using SharedLib;

namespace HeadlessServer;
public static class DependencyInjection
{
    public static void AddStartupConfigFactories(this IServiceCollection services)
    {
        services.AddSingleton<IOptions<StartupConfigPathing>>(x =>
            StartupConfigPathingFactory(x.GetRequiredService<RunOptions>()));

        services.AddSingleton<IOptions<StartupConfigReader>>(x =>
            StartupConfigReaderFactory(x.GetRequiredService<RunOptions>()));

        services.AddSingleton<IOptions<StartupConfigPid>>(x =>
            StartupConfigPidFactory(x.GetRequiredService<RunOptions>()));

        services.AddSingleton<IOptions<StartupConfigDiagnostics>>(x =>
            StartupConfigDiagnosticsFactory(x.GetRequiredService<RunOptions>()));

        services.AddSingleton<IOptions<StartupConfigNpcOverlay>>(x =>
            StartupConfigNpcOverlayFactory(x.GetRequiredService<RunOptions>()));
    }

    private static IOptions<StartupConfigPathing> StartupConfigPathingFactory(RunOptions options)
    => Options.Create<StartupConfigPathing>(
    new(options.Mode.ToString()!,
        options.Hostv1!, options.Portv1,
        options.Hostv3!, options.Portv3));

    private static IOptions<StartupConfigReader> StartupConfigReaderFactory(RunOptions options)
    => Options.Create<StartupConfigReader>(
        new() { Type = options.Reader.ToString() });

    private static IOptions<StartupConfigPid> StartupConfigPidFactory(RunOptions options)
    => Options.Create<StartupConfigPid>(
        new() { Id = options.Pid });

    private static IOptions<StartupConfigDiagnostics> StartupConfigDiagnosticsFactory(RunOptions options)
        => Options.Create<StartupConfigDiagnostics>(
            new() { Enabled = options.Diagnostics });

    private static IOptions<StartupConfigNpcOverlay> StartupConfigNpcOverlayFactory(RunOptions options)
    => Options.Create<StartupConfigNpcOverlay>(
        new()
        {
            Enabled = options.OverlayEnabled,
            ShowTargeting = options.OverlayTargeting,
            ShowSkinning = options.OverlaySkinning,
            ShowTargetVsAdd = options.OverlayTargetVsAdd,
        });
}
