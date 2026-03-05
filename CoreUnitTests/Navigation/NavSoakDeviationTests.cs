using Core.Navigation;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Navigation;

public class NavSoakDeviationTests
{
    [Fact]
    public void NavSoakWindow_HasDeviationFields()
    {
        NavSoakWindow window = new()
        {
            MaxRouteDeviation = 15.0f,
            AvgRouteDeviation = 8.5f
        };

        window.MaxRouteDeviation.Should().Be(15.0f);
        window.AvgRouteDeviation.Should().Be(8.5f);
    }

    [Fact]
    public void NavSoakWindow_DefaultDeviation_IsZero()
    {
        NavSoakWindow window = new();

        window.MaxRouteDeviation.Should().Be(0f, "default max deviation should be zero, not NaN");
        window.AvgRouteDeviation.Should().Be(0f, "default avg deviation should be zero, not NaN");
    }

    [Theory]
    [InlineData(new float[] { 5f }, 5f, 5f)]
    [InlineData(new float[] { 2f, 8f, 5f }, 8f, 5f)]
    [InlineData(new float[] { 1f, 1f, 1f, 1f }, 1f, 1f)]
    public void DeviationAccumulation_MatchesExpected(float[] samples, float expectedMax, float expectedAvg)
    {
        // Simulate what NavSoakMetricsService does: track max and running average
        float max = 0f;
        float sum = 0f;
        int count = 0;

        foreach (float sample in samples)
        {
            if (sample > max) max = sample;
            sum += sample;
            count++;
        }

        float avg = count > 0 ? sum / count : 0f;

        NavSoakWindow window = new()
        {
            MaxRouteDeviation = max,
            AvgRouteDeviation = avg
        };

        window.MaxRouteDeviation.Should().BeApproximately(expectedMax, 0.001f);
        window.AvgRouteDeviation.Should().BeApproximately(expectedAvg, 0.001f);
    }
}
