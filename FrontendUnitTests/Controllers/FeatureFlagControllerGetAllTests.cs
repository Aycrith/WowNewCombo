using Core.FeatureFlags;
using Frontend.Controllers;
using FrontendUnitTests.TestHelpers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace FrontendUnitTests.Controllers;

public sealed class FeatureFlagControllerGetAllTests
{
    [Fact]
    public void GetAll_IncludesStuckSensitivityAndHazardAvoidance()
    {
        FeatureFlagsOptions options = new()
        {
            StuckSensitivity = new StuckSensitivityOptions
            {
                Enabled = true,
                MinDistance = 0.11f,
                UnstuckAfterMs = 2222
            },
            HazardAvoidance = new HazardAvoidanceOptions
            {
                Enabled = true
            }
        };

        FeatureFlagService service = new(
            NullLogger<FeatureFlagService>.Instance,
            new FixedOptionsMonitor<FeatureFlagsOptions>(options),
            Options.Create(new FeatureFlagServiceOptions()));

        FeatureFlagController controller = new(service, NullLogger<FeatureFlagController>.Instance);

        IActionResult result = controller.GetAll();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        object payload = ok.Value!;
        object? features = payload.GetType().GetProperty("Features")?.GetValue(payload);
        Assert.NotNull(features);

        object? stuckSensitivity = features!.GetType().GetProperty("StuckSensitivity")?.GetValue(features);
        object? hazardAvoidance = features.GetType().GetProperty("HazardAvoidance")?.GetValue(features);

        Assert.NotNull(stuckSensitivity);
        Assert.NotNull(hazardAvoidance);

        object? unstuckAfterMs = stuckSensitivity!.GetType().GetProperty("UnstuckAfterMs")?.GetValue(stuckSensitivity);
        object? hazardEnabled = hazardAvoidance!.GetType().GetProperty("Enabled")?.GetValue(hazardAvoidance);

        Assert.Equal(2222, (int)unstuckAfterMs!);
        Assert.True((bool)hazardEnabled!);
    }
}
