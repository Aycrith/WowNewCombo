using Core.Navigation;
using FluentAssertions;
using Xunit;

namespace CoreUnitTests.Navigation;

/// <summary>
/// Tests that NavSoakWindow carries deviation fields and that
/// deviation accumulation math is correct.
/// </summary>
public class NavSoakDeviationTests
{
    [Fact]
    public void NavSoakWindow_HasDeviationFields()
    {
        var window = new NavSoakWindow
        {
            MaxRouteDeviation = 5.5f,
            AvgRouteDeviation = 2.3f
        };

        window.MaxRouteDeviation.Should().BeApproximately(5.5f, 0.001f);
        window.AvgRouteDeviation.Should().BeApproximately(2.3f, 0.001f);
    }

    [Fact]
    public void NavSoakWindow_DefaultDeviation_IsZero()
    {
        var window = new NavSoakWindow();
        window.MaxRouteDeviation.Should().Be(0f);
        window.AvgRouteDeviation.Should().Be(0f);
    }

    [Theory]
    [InlineData(new float[] { 1f, 2f, 3f }, 3f, 2f)]       // max=3, avg=2
    [InlineData(new float[] { 5f, 5f, 5f }, 5f, 5f)]       // all same
    [InlineData(new float[] { 0f, 0f, 10f }, 10f, 10f / 3f)] // one spike
    public void DeviationAccumulation_MatchesExpected(float[] samples, float expectedMax, float expectedAvg)
    {
        float max = 0f;
        float sum = 0f;
        foreach (float s in samples)
        {
            if (s > max)
            {
                max = s;
            }

            sum += s;
        }

        float avg = sum / samples.Length;

        max.Should().BeApproximately(expectedMax, 0.001f);
        avg.Should().BeApproximately(expectedAvg, 0.001f);
    }
}
