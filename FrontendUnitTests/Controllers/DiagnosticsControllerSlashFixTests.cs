using Core;
using Core.Startup;
using Frontend.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace FrontendUnitTests.Controllers;

public sealed class DiagnosticsControllerSlashFixTests
{
    [Theory]
    [InlineData("/reload", "/reload")]
    [InlineData(" /DCBINDINGS ", "/dcbindings")]
    [InlineData("/dcactions", "/dcactions")]
    public void TryNormalizeSupportedSlashCommand_AcceptsSafeListed(string input, string expected)
    {
        bool ok = DiagnosticsController.TryNormalizeSupportedSlashCommand(input, out string normalized, out string? error);

        Assert.True(ok);
        Assert.Equal(expected, normalized);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("reload")]
    [InlineData("/say hi")]
    [InlineData("/script RunScript()")]
    public void TryNormalizeSupportedSlashCommand_RejectsUnsupported(string input)
    {
        bool ok = DiagnosticsController.TryNormalizeSupportedSlashCommand(input, out string normalized, out string? error);

        Assert.False(ok);
        Assert.Equal(string.Empty, normalized);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void GetNavigationRuntime_WhenNavSoakServiceMissing_Returns503()
    {
        DiagnosticsController controller = CreateController(navSoakMetricsService: null);

        IActionResult result = controller.GetNavigationRuntime();

        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, obj.StatusCode);
    }

    [Fact]
    public void ResolveCastingSnapshot_WhenLiveSnapshotMissing_ReturnsFallback()
    {
        CastingRuntimeSnapshot snapshot = DiagnosticsController.ResolveCastingSnapshot(
            liveSnapshot: null,
            combatSnapshot: null);

        Assert.NotNull(snapshot);
        Assert.Equal(0, snapshot.CurrentActionNotDetectedCountWindow);
        Assert.Equal(0, snapshot.UIFeedbackNotDetectedCountWindow);
        Assert.Equal(0, snapshot.AmbiguousCastResolvedCountWindow);
        Assert.Equal(0, snapshot.InterruptedRetrySuppressedCountWindow);
        Assert.True(snapshot.WindowSeconds > 0);
    }

    [Fact]
    public void ResolveCastingSnapshot_WhenCombatSnapshotPresent_UsesCombatSnapshot()
    {
        CastingRuntimeSnapshot combatSnapshot = new(
            CurrentActionNotDetectedCountWindow: 3,
            UIFeedbackNotDetectedCountWindow: 1,
            AmbiguousCastResolvedCountWindow: 2,
            InterruptedRetrySuppressedCountWindow: 4,
            WindowSeconds: 120);

        CastingRuntimeSnapshot snapshot = DiagnosticsController.ResolveCastingSnapshot(
            liveSnapshot: null,
            combatSnapshot: combatSnapshot);

        Assert.Equal(combatSnapshot, snapshot);
    }

    private static DiagnosticsController CreateController(Core.Navigation.NavSoakMetricsService? navSoakMetricsService)
    {
        return new DiagnosticsController(
            NullLogger<DiagnosticsController>.Instance,
            keyBindingsReader: null!,
            slotValidator: null!,
            textureReader: null!,
            botController: null!,
            exec: null!,
            addonConfigurator: null!,
            addonReader: null!,
            wowInput: null!,
            cursorScan: null!,
            addonBits: null!,
            bagReader: null!,
            equipmentReader: null!,
            loggerFactory: NullLoggerFactory.Instance,
            systemDiagnostics: null!,
            startupOptions: Options.Create(new StartupOptions()),
            navSoakMetricsService: navSoakMetricsService,
            featureFlagService: null,
            castingHandler: null,
            goapEventHistory: null);
    }
}
