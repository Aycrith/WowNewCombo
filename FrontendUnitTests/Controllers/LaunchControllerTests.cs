using System;
using System.Collections.Generic;

using Core;
using Core.Launch;

using Frontend.Controllers;
using Frontend.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace FrontendUnitTests.Controllers;

public sealed class LaunchControllerTests
{
    [Fact]
    public void GetStatus_EnrichesSnapshotWithProfileLoadAndActionBarMetadata()
    {
        LaunchOverrideState overrides = new(NullLogger<LaunchOverrideState>.Instance);
        overrides.SetBypass(LaunchSubsystem.ActionBar, true, "Known slot drift", "test");

        FakeBotStartGuard guard = new()
        {
            Snapshot = new LaunchReadinessSnapshot(
                IsLaunchReady: false,
                CanStartBot: false,
                TimestampUtc: DateTimeOffset.UtcNow,
                Checks:
                [
                    new LaunchSubsystemCheck(
                        LaunchSubsystem.ActionBar,
                        LaunchStatus.Error,
                        "Action Bar",
                        "Slot 2 is empty but expected 'Shadow Bolt'",
                        IsRequired: true,
                        IsBlocking: true,
                        TimestampUtc: DateTimeOffset.UtcNow)
                ],
                Overrides: overrides.Snapshot())
        };

        LaunchReadinessService readiness = new(
            NullLogger<LaunchReadinessService>.Instance,
            guard,
            overrides);

        ProfileLoadTelemetryService telemetry = new();
        telemetry.RecordFailure(
            "BloodElf_Warlock_1-70_TBC.json",
            "Undead_Rogue_1-70.json",
            "WrongProfileLoaded",
            "Requested warlock profile but rogue remained active.",
            "corr-123");

        FakeBotController botController = new()
        {
            SelectedClassFilename = "Undead_Rogue_1-70.json"
        };

        LaunchController controller = new(
            NullLogger<LaunchController>.Instance,
            readiness,
            overrides,
            botController,
            null!,
            null!,
            telemetry);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        ActionResult<LaunchReadinessSnapshot> response = controller.GetStatus();
        OkObjectResult ok = Assert.IsType<OkObjectResult>(response.Result);
        LaunchReadinessSnapshot snapshot = Assert.IsType<LaunchReadinessSnapshot>(ok.Value);
        Assert.Equal("BloodElf_Warlock_1-70_TBC.json", snapshot.RequestedProfile);
        Assert.Equal("Undead_Rogue_1-70.json", snapshot.AppliedProfile);
        Assert.Equal("WrongProfileLoaded", snapshot.ProfileLoadFailureKind);
        Assert.Equal(1, snapshot.ActionBarIssueCount);
        Assert.True(snapshot.ActionBarBypassActive);
        Assert.Equal("Known slot drift", snapshot.ActionBarBypassReason);
        Assert.False(snapshot.KeyBindingsBypassActive);
        Assert.False(snapshot.AllowStartWithWarningsActive);
        Assert.False(snapshot.EmergencyBypassAllActive);
        Assert.True(snapshot.AnyBypassActive);
    }
}
