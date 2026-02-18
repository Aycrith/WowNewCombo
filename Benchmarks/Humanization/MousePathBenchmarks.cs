using BenchmarkDotNet.Attributes;

using Core.Humanization;

using SixLabors.ImageSharp;

namespace Benchmarks.Humanization;

[MemoryDiagnoser]
public class MousePathBenchmarks
{
    private Point start;
    private Point endShort;
    private Point endLong;

    private Point[] shortBuffer = [];
    private Point[] longBuffer = [];

    [GlobalSetup]
    public void Setup()
    {
        start = new Point(200, 200);
        endShort = new Point(260, 235);  // ~70px
        endLong = new Point(900, 700);   // ~860px

        shortBuffer = new Point[128];
        longBuffer = new Point[512];
    }

    [Benchmark]
    public int ShortPath()
    {
        return HumanizedMousePath.BuildPath(
            start,
            endShort,
            stepsPerMovement: 15,
            curveIntensity: 0.30,
            microJitterPixels: 1,
            overshootProbability: 0.15,
            buffer: shortBuffer);
    }

    [Benchmark]
    public int LongPath()
    {
        return HumanizedMousePath.BuildPath(
            start,
            endLong,
            stepsPerMovement: 40,
            curveIntensity: 0.35,
            microJitterPixels: 2,
            overshootProbability: 0.25,
            buffer: longBuffer);
    }
}
