using Core;
using Core.Database;
using Core.Testing;

using Frontend.Controllers;

using Game;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using SharedLib;

using System;
using System.Runtime.CompilerServices;

using Xunit;

namespace FrontendUnitTests.Controllers;

public sealed class TestControllerFrameValidationTests
{
    [Fact]
    public void ValidateFrames_WhenWarlockProfileLoaded_UsesWarlockExpectation()
    {
        TestController controller = CreateController(
            profileName: "BloodElf_Warlock_1-70_TBC.json",
            actualRace: UnitRace.BloodElf,
            actualClass: UnitClass.Warlock);

        IActionResult result = controller.ValidateFrames();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        TestResult payload = Assert.IsType<TestResult>(ok.Value);

        TestCheck classCheck = Assert.Single(payload.Checks, c => c.Name == "Frame 46 (Class)");
        Assert.True(classCheck.Passed);
        Assert.Equal("9", classCheck.Expected);
        Assert.Equal("Expected Warlock", classCheck.Message);
    }

    [Fact]
    public void ValidateFrames_WhenRogueProfileLoaded_UsesRogueExpectation()
    {
        TestController controller = CreateController(
            profileName: "BloodElf_Rogue_8-60_TBC.json",
            actualRace: UnitRace.BloodElf,
            actualClass: UnitClass.Rogue);

        IActionResult result = controller.ValidateFrames();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        TestResult payload = Assert.IsType<TestResult>(ok.Value);

        TestCheck classCheck = Assert.Single(payload.Checks, c => c.Name == "Frame 46 (Class)");
        Assert.True(classCheck.Passed);
        Assert.Equal("4", classCheck.Expected);
        Assert.Equal("Expected Rogue", classCheck.Message);
    }

    [Fact]
    public void ValidateFrames_WhenProfileClassDoesNotMatchPlayer_FailsClassCheck()
    {
        TestController controller = CreateController(
            profileName: "BloodElf_Rogue_8-60_TBC.json",
            actualRace: UnitRace.BloodElf,
            actualClass: UnitClass.Warlock);

        IActionResult result = controller.ValidateFrames();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        TestResult payload = Assert.IsType<TestResult>(ok.Value);

        TestCheck classCheck = Assert.Single(payload.Checks, c => c.Name == "Frame 46 (Class)");
        Assert.False(classCheck.Passed);
        Assert.Equal("4", classCheck.Expected);
        Assert.Equal("9", classCheck.Actual);
        Assert.Equal("Expected Rogue", classCheck.Message);
    }

    private static TestController CreateController(string profileName, UnitRace actualRace, UnitClass actualClass)
    {
        FakeAddonDataProvider addonData = new();
        addonData.Data[1] = 502500;
        addonData.Data[2] = 643000;
        addonData.Data[8] = 1;
        addonData.Data[10] = 500;
        addonData.Data[11] = 450;
        addonData.Data[12] = 200;
        addonData.Data[13] = 150;
        addonData.Data[46] = BuildRaceClassVersionValue(actualRace, actualClass);
        addonData.Data[323] = 2000001;

        AddonBits bits = new();
        bits.Update(addonData);

        PlayerReader playerReader = new(
            addonData,
            (WorldMapAreaDB)RuntimeHelpers.GetUninitializedObject(typeof(WorldMapAreaDB)),
            (AreaDB)RuntimeHelpers.GetUninitializedObject(typeof(AreaDB)),
            bits,
            new SpellInRange(),
            new Stance());
        playerReader.Level.ForceUpdate(20);
        playerReader.UIMapId.ForceUpdate(1941);

        FakeBotController botController = new()
        {
            SelectedClassFilename = profileName
        };

        return new TestController(
            NullLogger<TestController>.Instance,
            addonData,
            playerReader,
            bits,
            new NullWowScreen(),
            (WowProcess)RuntimeHelpers.GetUninitializedObject(typeof(WowProcess)),
            (WowProcessInput)RuntimeHelpers.GetUninitializedObject(typeof(WowProcessInput)),
            botController);
    }

    private static int BuildRaceClassVersionValue(UnitRace race, UnitClass playerClass)
    {
        const int tbcVersion = 2;
        return ((int)race * 10000) + ((int)playerClass * 100) + tbcVersion;
    }

    private sealed class FakeAddonDataProvider : IAddonDataProvider
    {
        public int[] Data { get; } = new int[324];

        public void Dispose()
        {
        }

        public void InitFrames(DataFrame[] frames)
        {
        }

        public void UpdateData()
        {
        }
    }
}
