using Serilog;
using Serilog.Extensions.Logging;
using SharedLib.NpcFinder;
using System.Threading;
using System.Threading.Tasks;

namespace CoreTests
{
    class Program
    {
        private static Microsoft.Extensions.Logging.ILogger logger;

        private static void CreateLogger()
        {
            var logConfig = new LoggerConfiguration()
                .WriteTo.File("names.log")
                .WriteTo.Debug()
                .CreateLogger();

            Log.Logger = logConfig;
            logger = new SerilogLoggerProvider(Log.Logger).CreateLogger(nameof(Program));
        }

        public static void Main()
        {
            CreateLogger();

            //var types = NpcNames.Enemy;
            //var types = NpcNames.Corpse;
            var types = NpcNames.Enemy | NpcNames.Neutral;
            //var types = NpcNames.Friendly | NpcNames.Neutral;

            var test = new Test_NpcNameFinder(logger, types, true);
            int count = 100;
            int i = 0;
            while (i < count)
            {
                test.Execute();
                i++;
                Thread.Sleep(150);
            }

            //MainAsync().GetAwaiter().GetResult();
        }

        private static async Task MainAsync()
        {
            var test = new Test_MouseClicks(logger);
            await test.Execute();
        }
    }
}
