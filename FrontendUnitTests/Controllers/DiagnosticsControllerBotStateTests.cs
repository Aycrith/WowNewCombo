using Core.Startup;

using Frontend.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace FrontendUnitTests.Controllers;

public sealed class DiagnosticsControllerBotStateTests
{
    [Fact]
    public void GetSoakCurrent_WhenServiceNull_Returns503()
    {
        DiagnosticsController controller = CreateController(navSoakService: null);

        IActionResult result = controller.GetSoakCurrent();

        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, obj.StatusCode);
    }

    [Fact]
    public void FlushSoak_WhenServiceNull_Returns503()
    {
        DiagnosticsController controller = CreateController(navSoakService: null);

        IActionResult result = controller.FlushSoak().GetAwaiter().GetResult();

        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, obj.StatusCode);
    }

    [Fact]
    public void GetNavigationRuntime_WhenServiceNull_Returns503()
    {
        DiagnosticsController controller = CreateController(navSoakService: null);

        IActionResult result = controller.GetNavigationRuntime();

        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, obj.StatusCode);
    }

    private static DiagnosticsController CreateController(Core.Navigation.NavSoakMetricsService? navSoakService)
    {
        return new DiagnosticsController(
            NullLogger<DiagnosticsController>.Instance,
            keyBindingsReader: null!,
            slotValidator: null!,
            textureReader: null!,
            botController: null!,
            addonReader: null!,
            bagReader: null!,
            systemDiagnostics: null!,
            startupOptions: Options.Create(new StartupOptions()),
            navSoakMetricsService: navSoakService,
            featureFlagService: null,
            castingHandler: null,
            goapEventHistory: null);
    }
}
