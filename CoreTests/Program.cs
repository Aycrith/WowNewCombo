using Serilog;
using Serilog.Extensions.Logging;
using SharedLib.NpcFinder;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using Core;

#pragma warning disable 0162

namespace CoreTests
{
    class Program
    {
        private static Microsoft.Extensions.Logging.ILogger logger;

        private const bool LogSelf = false;

        public static void Main()
        {
            var logConfig = new LoggerConfiguration()
                .WriteTo.File("names.log")
                .WriteTo.Debug()
                .CreateLogger();

            Log.Logger = logConfig;
            logger = new SerilogLoggerProvider(Log.Logger).CreateLogger(nameof(Program));

            Test_NPCNameFinder();
            //Test_Input();
            //Test_CursorGrabber();
        }

        private static void Test_NPCNameFinder()
        {
            //NpcNames types = NpcNames.Enemy;
            //NpcNames types = NpcNames.Corpse;
            NpcNames types = NpcNames.Enemy | NpcNames.Neutral;
            //NpcNames types = NpcNames.Friendly | NpcNames.Neutral;

            using Test_NpcNameFinder test = new(logger, types, true);
            int count = 100;
            int i = 0;

            Stopwatch stopwatch = new();
            double[] sample = new double[count];

            Log.Logger.Information($"running {count} samples...");

            while (i < count)
            {
                if (LogSelf)
                    stopwatch.Restart();

                test.Execute();

                if (LogSelf)
                    sample[i] = stopwatch.ElapsedMilliseconds;

                i++;
                Thread.Sleep(150);
            }

            if (LogSelf)
                Log.Logger.Information($"sample: {count} | avg: {sample.Average(),0:0.00} | min: {sample.Min(),0:0.00} | max: {sample.Max(),0:0.00} | total: {sample.Sum()}");
        }

        private static void Test_Input()
        {
            Test_Input test = new(logger);
            test.Mouse_Movement();
            test.Mouse_Clicks();
            test.Clipboard();
        }

        private static void Test_CursorGrabber()
        {
            CursorClassifier classifier = new();
            int i = 5;
            while (i > 0)
            {
                Thread.Sleep(1000);

                classifier.Classify(out CursorType cursorType);
                Log.Logger.Information($"{cursorType.ToStringF()}");

                i--;
            }
        }

    }
}
