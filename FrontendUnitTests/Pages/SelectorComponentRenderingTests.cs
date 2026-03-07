using Bunit;

using Core;

using Frontend.Pages;

using FrontendUnitTests.Controllers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

using System;
using System.IO;

using Xunit;

namespace FrontendUnitTests.Pages;

public sealed class SelectorComponentRenderingTests : IDisposable
{
    private readonly TestContext testContext = new();
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), $"wowclassicgrindbot-frontendtests-{Guid.NewGuid():N}");

    public SelectorComponentRenderingTests()
    {
        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(Path.Combine(tempRoot, "class"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "path"));

        testContext.Services.AddSingleton<ILogger>(NullLogger.Instance);
        testContext.Services.AddSingleton(new DataConfig { Root = tempRoot });
        testContext.Services.AddSingleton<IBotController>(new FakeBotController());
        testContext.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void PathSelectorComponent_WhenNoPathsExist_RendersWithoutThrowing()
    {
        IRenderedComponent<PathSelectorComponent> cut = testContext.RenderComponent<PathSelectorComponent>();

        string markup = cut.Markup;

        Assert.Contains("Path Profile", markup);
        Assert.Contains("Load Path", markup);
    }

    public void Dispose()
    {
        testContext.Dispose();

        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, true);
        }
    }
}
