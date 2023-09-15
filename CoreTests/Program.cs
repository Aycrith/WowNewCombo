using Serilog;
using Serilog.Extensions.Logging;
using SharedLib.NpcFinder;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using Core;
using System;
using Microsoft.Extensions.Logging;

#pragma warning disable 0162

namespace CoreTests;

sealed class Program
{
    private static Microsoft.Extensions.Logging.ILogger logger;
    private static ILoggerFactory loggerFactory;

    private const bool LogOverallTimes = false;
    private const int delay = 150;

    public static void Main()
    {
        var logConfig = new LoggerConfiguration()
            .WriteTo.File("names.log")
            .WriteTo.Debug()
            .CreateLogger();

        Log.Logger = logConfig;
        logger = new SerilogLoggerProvider(Log.Logger).CreateLogger(nameof(Program));

        loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders().AddSerilog();
        });

        Test_NPCNameFinder();
        //Test_Input();
        //Test_CursorGrabber();
        //Test_CursorCompare();
        //Test_MinimapNodeFinder();
        //Test_FindTargetByCursor();
    }

    private static void Test_NPCNameFinder()
    {
        //NpcNames types = NpcNames.Enemy;
        //NpcNames types = NpcNames.Corpse;
        NpcNames types = NpcNames.Enemy | NpcNames.Neutral;
        //NpcNames types = NpcNames.Enemy | NpcNames.Neutral | NpcNames.NamePlate;
        //NpcNames types = NpcNames.Friendly | NpcNames.Neutral;

        using Test_NpcNameFinder test = new(logger, loggerFactory, types);
        int count = 100;
        int i = 0;

        long timestamp = Stopwatch.GetTimestamp();
        double[] sample = new double[count];

        Log.Logger.Information($"running {count} samples...");

        while (i < count)
        {
            if (LogOverallTimes)
                timestamp = Stopwatch.GetTimestamp();

            test.Execute();

            if (LogOverallTimes)
                sample[i] = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;

            i++;
            Thread.Sleep(delay);
        }

        if (LogOverallTimes)
            Log.Logger.Information($"sample: {count} | avg: {sample.Average(),0:0.00} | min: {sample.Min(),0:0.00} | max: {sample.Max(),0:0.00} | total: {sample.Sum()}");
    }

    private static void Test_Input()
    {
        Test_Input test = new(logger, loggerFactory);
        test.Mouse_Movement();
        test.Mouse_Clicks();
        test.Clipboard();
    }

    private static void Test_CursorGrabber()
    {
        using CursorClassifier classifier = new();
        int i = 5;
        while (i > 0)
        {
            Thread.Sleep(1000);

            classifier.Classify(out CursorType cursorType, out _);
            Log.Logger.Information($"{cursorType.ToStringF()}");

            i--;
        }
    }

    private static void Test_CursorCompare()
    {
        using CursorClassifier classifier = new();
        const int count = 50;
        int i = 0;

        Span<double> times = stackalloc double[count];

        while (i < count)
        {
            Thread.Sleep(100);

            long startTime = Stopwatch.GetTimestamp();

            classifier.Classify(out CursorType cursorType, out double similarity);

            times[i] = Stopwatch.GetElapsedTime(startTime).TotalMilliseconds;
            //Log.Logger.Information($"{cursorType.ToStringF()} {similarity} {times[i]:F6}ms");
            i++;
        }

        double sum = 0;
        double max = double.MinValue;
        double min = double.MaxValue;

        for (i = 0; i < times.Length - 1; i++)
        {
            double val = times[i];
            sum += val;
            if (val > max) max = val;
            else if (val < min) min = val;
        }

        Log.Logger.Information($"min:{min:F6} | max: {max:F5} | avg:{(sum / count):F6}");
    }

    private static void Test_MinimapNodeFinder()
    {
        using Test_MinimapNodeFinder test = new(logger, loggerFactory);

        int count = 100;
        int i = 0;

        long timestamp = Stopwatch.GetTimestamp();
        double[] sample = new double[count];

        Log.Logger.Information($"running {count} samples...");

        while (i < count)
        {
            if (LogOverallTimes)
                timestamp = Stopwatch.GetTimestamp();

            test.Execute();

            if (LogOverallTimes)
                sample[i] = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;

            i++;
            Thread.Sleep(delay);
        }

        if (LogOverallTimes)
            Log.Logger.Information($"sample: {count} | avg: {sample.Average(),0:0.00} | min: {sample.Min(),0:0.00} | max: {sample.Max(),0:0.00} | total: {sample.Sum()}");
    }

    private static void Test_FindTargetByCursor()
    {
        //CursorType cursorType = CursorType.Kill;
        Span<CursorType> cursorType = stackalloc[] { CursorType.Vendor };

        //NpcNames types = NpcNames.Enemy;
        //NpcNames types = NpcNames.Corpse;
        //NpcNames types = NpcNames.Enemy | NpcNames.Neutral;
        NpcNames types = NpcNames.Friendly | NpcNames.Neutral;

        using Test_NpcNameFinder test = new(logger, loggerFactory, types);

        int count = 2;
        int i = 0;

        while (i < count)
        {
            test.Execute();
            if (test.Execute_FindTargetBy(cursorType))
            {
                break;
            }

            i++;
            Thread.Sleep(delay);
        }
    }
}
