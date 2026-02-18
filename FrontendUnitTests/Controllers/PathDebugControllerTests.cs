using Core.FeatureFlags;
using FrontendUnitTests.TestHelpers;

using Frontend.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using System;

using Xunit;

namespace FrontendUnitTests.Controllers;

public sealed class PathDebugControllerTests
{
    [Fact]
    public void Route_WhenDebugModeDisabled_Returns403()
    {
        FeatureFlagsOptions options = new()
        {
            DebugMode = false
        };

        FeatureFlagService flags = new(
            NullLogger<FeatureFlagService>.Instance,
            new FixedOptionsMonitor<FeatureFlagsOptions>(options),
            Options.Create(new FeatureFlagServiceOptions()));

        PathDebugController controller = new(
            NullLogger<PathDebugController>.Instance,
            flags,
            patherService: null!,
            hazardProvider: null!);

        DebugPathRequest request = new(
            FromX: 0,
            FromY: 0,
            FromZ: 0,
            ToX: 1,
            ToY: 1,
            ToZ: 0);

        IActionResult result = controller.Route(mapId: 0, request);

        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, obj.StatusCode);
    }

    [Fact]
    public void Compare_WhenDebugModeDisabled_Returns403()
    {
        FeatureFlagsOptions options = new()
        {
            DebugMode = false
        };

        FeatureFlagService flags = new(
            NullLogger<FeatureFlagService>.Instance,
            new FixedOptionsMonitor<FeatureFlagsOptions>(options),
            Options.Create(new FeatureFlagServiceOptions()));

        PathDebugController controller = new(
            NullLogger<PathDebugController>.Instance,
            flags,
            patherService: null!,
            hazardProvider: null!);

        DebugPathCompareRequest request = new(
            FromX: 0,
            FromY: 0,
            FromZ: 0,
            ToX: 1,
            ToY: 1,
            ToZ: 0);

        IActionResult result = controller.Compare(mapId: 0, request);

        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, obj.StatusCode);
    }

}
