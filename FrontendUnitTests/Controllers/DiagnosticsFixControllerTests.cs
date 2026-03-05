using System.Reflection;
using System.Linq;
using System.Threading.Tasks;

using Frontend.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace FrontendUnitTests.Controllers;

public sealed class DiagnosticsFixControllerTests
{
    [Fact]
    public void Controller_UsesExpectedBaseRoute()
    {
        RouteAttribute? route = typeof(DiagnosticsFixController).GetCustomAttribute<RouteAttribute>();

        Assert.NotNull(route);
        Assert.Equal("api/diagnostics", route.Template);
    }

    [Theory]
    [InlineData(nameof(DiagnosticsFixController.TryInteractMailbox), "mailbox/interact")]
    [InlineData(nameof(DiagnosticsFixController.FixBindings), "fix/bindings")]
    [InlineData(nameof(DiagnosticsFixController.FixNumberKeys), "fix/numberkeys")]
    [InlineData(nameof(DiagnosticsFixController.FixActions), "fix/actions")]
    [InlineData(nameof(DiagnosticsFixController.FixSyncBar), "fix/syncbar")]
    [InlineData(nameof(DiagnosticsFixController.FixReload), "fix/reload")]
    [InlineData(nameof(DiagnosticsFixController.FixSlash), "fix/slash")]
    [InlineData(nameof(DiagnosticsFixController.FixFlush), "fix/flush")]
    [InlineData(nameof(DiagnosticsFixController.FixInitState), "fix/initstate")]
    [InlineData(nameof(DiagnosticsFixController.FixPlace), "fix/place")]
    [InlineData(nameof(DiagnosticsFixController.FixAll), "fix/all")]
    [InlineData(nameof(DiagnosticsFixController.GetInputMode), "input-mode")]
    [InlineData(nameof(DiagnosticsFixController.SetInputMode), "input-mode")]
    public void Action_UsesExpectedRouteTemplate(string methodName, string template)
    {
        MethodInfo? method = typeof(DiagnosticsFixController).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);

        HttpMethodAttribute? routeAttribute = method!
            .GetCustomAttributes(inherit: true)
            .OfType<HttpMethodAttribute>()
            .SingleOrDefault();

        Assert.NotNull(routeAttribute);
        Assert.Equal(template, routeAttribute!.Template);
    }

    [Fact]
    public async Task FixSlash_WhenCommandUnsupported_Returns400()
    {
        DiagnosticsFixController controller = CreateController();
        SlashCommandRequest request = new("/script RunScript()");

        IActionResult result = await controller.FixSlash(request);

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
        SlashCommandResult payload = Assert.IsType<SlashCommandResult>(badRequest.Value);
        Assert.False(payload.Success);
        Assert.Equal("Rejected", payload.DispatchPath);
        Assert.Equal(request.Command, payload.Command);
        Assert.Contains("Unsupported slash command", payload.Error);
    }

    private static DiagnosticsFixController CreateController()
    {
        return new DiagnosticsFixController(
            NullLogger<DiagnosticsFixController>.Instance,
            botController: null!,
            exec: null!,
            addonConfigurator: null!,
            addonReader: null!,
            wowInput: null!,
            cursorScan: null!,
            addonBits: null!,
            bagReader: null!,
            equipmentReader: null!,
            loggerFactory: NullLoggerFactory.Instance);
    }
}
