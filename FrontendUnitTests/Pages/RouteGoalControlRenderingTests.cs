using Bunit;

using Core;

using Frontend.Pages;

using FrontendUnitTests.Controllers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using System.Linq;

using Xunit;

namespace FrontendUnitTests.Pages;

public sealed class RouteGoalControlRenderingTests : TestContext
{
    [Fact]
    public void RouteGoalControl_WhenProfileLoaded_RendersInteractiveButtons()
    {
        FakeBotController botController = new()
        {
            SelectedClassFilename = "BloodElf_Warlock_1-70_TBC.json",
            ClassConfig = new ClassConfiguration
            {
                FileName = "BloodElf_Warlock_1-70_TBC.json",
                Paths =
                [
                    new PathSettings
                    {
                        Id = 10,
                        PathFilename = "15-20_Ghostlands_Windrunner.json",
                        PathThereAndBack = true
                    }
                ]
            },
            PathFileList =
            [
                "15-20_Ghostlands_Windrunner.json",
                "alternate-ghostlands.json"
            ]
        };

        Services.AddSingleton<IBotController>(botController);
        Services.AddSingleton<IBotRouteControlService>(new BotRouteControlService(
            NullLogger<BotRouteControlService>.Instance,
            botController,
            new FakeBotStartGuard()));

        IRenderedComponent<RouteGoalControl> cut = RenderComponent<RouteGoalControl>();

        Assert.Contains("Route Goal Control", cut.Markup);
        Assert.Contains("Apply Route", cut.Markup);
        Assert.Contains("Apply and Resume", cut.Markup);
        Assert.Contains("15-20_Ghostlands_Windrunner.json", cut.Markup);
    }

    [Fact]
    public void RouteGoalControl_WhenApplyAndResumeClicked_RendersResumeFeedback()
    {
        FakeBotController botController = new()
        {
            IsBotActive = true,
            SelectedClassFilename = "BloodElf_Warlock_1-70_TBC.json",
            ClassConfig = new ClassConfiguration
            {
                FileName = "BloodElf_Warlock_1-70_TBC.json",
                Paths =
                [
                    new PathSettings
                    {
                        Id = 10,
                        PathFilename = "15-20_Ghostlands_Windrunner.json",
                        PathThereAndBack = true
                    }
                ]
            },
            PathFileList =
            [
                "15-20_Ghostlands_Windrunner.json",
                "alternate-ghostlands.json"
            ]
        };

        Services.AddSingleton<IBotController>(botController);
        Services.AddSingleton<IBotRouteControlService>(new BotRouteControlService(
            NullLogger<BotRouteControlService>.Instance,
            botController,
            new FakeBotStartGuard()));

        IRenderedComponent<RouteGoalControl> cut = RenderComponent<RouteGoalControl>();

        cut.FindAll("select")[1].Change("alternate-ghostlands.json");
        cut.FindAll("button").First(button => button.TextContent.Contains("Apply and Resume")).Click();

        Assert.Contains("Bot resumed on the new route.", cut.Markup);
        Assert.True(botController.IsBotActive);
    }
}
