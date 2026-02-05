using Core.Humanization;

using System;

using Xunit;

namespace CoreUnitTests.Humanization;

public sealed class HumanizedRandomTests
{
    [Fact]
    public void NextGaussian_ProducesExpectedMeanAndStdDev_Approximately()
    {
        const double mean = 100.0;
        const double stdDev = 15.0;

        Random random = new(123456);

        const int n = 200_000;
        double sum = 0.0;
        double sumSquares = 0.0;

        for (int i = 0; i < n; i++)
        {
            double v = HumanizedRandom.NextGaussian(random, mean, stdDev);
            sum += v;
            sumSquares += v * v;
        }

        double sampleMean = sum / n;
        double sampleVariance = (sumSquares / n) - (sampleMean * sampleMean);
        if (sampleVariance < 0)
        {
            sampleVariance = 0;
        }

        double sampleStdDev = Math.Sqrt(sampleVariance);

        Assert.InRange(sampleMean, mean - 0.5, mean + 0.5);
        Assert.InRange(sampleStdDev, stdDev - 0.8, stdDev + 0.8);
    }

    [Fact]
    public void NextGaussianInt_ClampsToRange()
    {
        Random random = new(123);
        for (int i = 0; i < 10_000; i++)
        {
            int v = HumanizedRandom.NextGaussianInt(random, mean: 100, stdDev: 50, min: 10, max: 20);
            Assert.InRange(v, 10, 20);
        }
    }
}

