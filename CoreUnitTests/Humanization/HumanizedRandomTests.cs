using Core.Humanization;

using System.Collections.Generic;
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
    public void NextGaussian_ProducesNearNormalShape_ByMomentsAndQuantiles()
    {
        const double mean = 100.0;
        const double stdDev = 15.0;
        const int n = 120_000;

        Random random = new(987654);
        double[] samples = new double[n];

        for (int i = 0; i < n; i++)
        {
            samples[i] = HumanizedRandom.NextGaussian(random, mean, stdDev);
        }

        Array.Sort(samples);

        double sampleMean = Mean(samples);
        double sampleStdDev = StdDev(samples, sampleMean);
        (double skewness, double excessKurtosis) = ShapeMoments(samples, sampleMean, sampleStdDev);

        double p5 = Quantile(samples, 0.05);
        double p50 = Quantile(samples, 0.50);
        double p95 = Quantile(samples, 0.95);

        Assert.InRange(sampleMean, mean - 0.6, mean + 0.6);
        Assert.InRange(sampleStdDev, stdDev - 1.0, stdDev + 1.0);

        Assert.InRange(skewness, -0.10, 0.10);
        Assert.InRange(excessKurtosis, -0.20, 0.20);

        Assert.InRange(p5, mean - (2.0 * stdDev), mean - (1.0 * stdDev));
        Assert.InRange(p50, mean - 0.8, mean + 0.8);
        Assert.InRange(p95, mean + (1.0 * stdDev), mean + (2.0 * stdDev));
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

    private static double Mean(IReadOnlyList<double> samples)
    {
        double sum = 0.0;
        for (int i = 0; i < samples.Count; i++)
        {
            sum += samples[i];
        }

        return sum / samples.Count;
    }

    private static double StdDev(IReadOnlyList<double> samples, double mean)
    {
        double sum = 0.0;
        for (int i = 0; i < samples.Count; i++)
        {
            double d = samples[i] - mean;
            sum += d * d;
        }

        return Math.Sqrt(sum / samples.Count);
    }

    private static (double Skewness, double ExcessKurtosis) ShapeMoments(IReadOnlyList<double> samples, double mean, double stdDev)
    {
        if (stdDev <= 0)
        {
            return (0, 0);
        }

        double m3 = 0.0;
        double m4 = 0.0;
        for (int i = 0; i < samples.Count; i++)
        {
            double z = (samples[i] - mean) / stdDev;
            double z2 = z * z;
            m3 += z2 * z;
            m4 += z2 * z2;
        }

        double n = samples.Count;
        double skewness = m3 / n;
        double excessKurtosis = (m4 / n) - 3.0;
        return (skewness, excessKurtosis);
    }

    private static double Quantile(IReadOnlyList<double> sorted, double p)
    {
        if (sorted.Count == 0)
        {
            return 0;
        }

        if (p <= 0)
        {
            return sorted[0];
        }

        if (p >= 1)
        {
            return sorted[^1];
        }

        double index = p * (sorted.Count - 1);
        int lower = (int)index;
        int upper = Math.Min(lower + 1, sorted.Count - 1);
        double fraction = index - lower;
        return sorted[lower] + ((sorted[upper] - sorted[lower]) * fraction);
    }
}
