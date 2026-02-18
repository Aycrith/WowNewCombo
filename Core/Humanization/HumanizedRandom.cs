using System;
using System.Runtime.CompilerServices;

namespace Core.Humanization;

public static class HumanizedRandom
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double NextGaussian(double mean, double stdDev)
    {
        return NextGaussian(Random.Shared, mean, stdDev);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double NextGaussian(Random random, double mean, double stdDev)
    {
        if (random == null)
        {
            throw new ArgumentNullException(nameof(random));
        }

        // Box-Muller transform
        double u1 = 1.0 - random.NextDouble();
        double u2 = 1.0 - random.NextDouble();
        double normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        return mean + (stdDev * normal);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NextGaussianInt(double mean, double stdDev, int min, int max)
    {
        return NextGaussianInt(Random.Shared, mean, stdDev, min, max);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NextGaussianInt(Random random, double mean, double stdDev, int min, int max)
    {
        int result = (int)NextGaussian(random, mean, stdDev);
        return Math.Clamp(result, min, max);
    }
}
