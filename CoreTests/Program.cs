using Serilog;
using Serilog.Extensions.Logging;
using SharedLib.NpcFinder;
using System.Threading;

namespace CoreTests
{
    class Program
    {
        private static Microsoft.Extensions.Logging.ILogger logger;

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
        }

        private static void Test_NPCNameFinder()
        {
            //NpcNames types = NpcNames.Enemy;
            //NpcNames types = NpcNames.Corpse;
            NpcNames types = NpcNames.Enemy | NpcNames.Neutral;
            //NpcNames types = NpcNames.Friendly | NpcNames.Neutral;

            Test_NpcNameFinder test = new(logger, types, true);
            int count = 100;
            int i = 0;
            while (i < count)
            {
                test.Execute();
                i++;
                Thread.Sleep(150);
            }
        }

        private static void Test_Input()
        {
            Test_Input test = new(logger);
            test.Mouse_Movement();
            test.Mouse_Clicks();
            test.Clipboard();
        }
    }
}
