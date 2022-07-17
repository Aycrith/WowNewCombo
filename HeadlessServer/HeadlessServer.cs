using CommandLine;
using Core;
using Microsoft.Extensions.Logging;

namespace HeadlessServer
{
    public class HeadlessServer
    {
        private readonly ILogger logger;
        private readonly IBotController botController;
        private readonly IAddonReader addonReader;
        private readonly ExecGameCommand exec;
        private readonly AddonConfigurator addonConfigurator;
        private readonly Wait wait;

        public HeadlessServer(ILogger logger, IBotController botController,
            IAddonReader addonReader, ExecGameCommand exec, AddonConfigurator addonConfigurator, Wait wait)
        {
            this.logger = logger;
            this.botController = botController;
            this.addonReader = addonReader;
            this.exec = exec;
            this.addonConfigurator = addonConfigurator;
            this.wait = wait;
        }

        public void Run(ParserResult<RunOptions> options)
        {
            logger.LogInformation($"Running {nameof(HeadlessServer)}!");

            if (options.Value.Pid != -1)
            {
                logger.LogInformation($"Attached pid={options.Value.Pid}");
            }

            InitState();

            botController.LoadClassProfile(options.Value.ClassConfig!);

            botController.ToggleBotStatus();
        }

        private void InitState()
        {
            addonReader.FullReset();
            exec.Run($"/{addonConfigurator.Config.CommandFlush}");

            int actionbarCost;
            int spellBook;

            do
            {
                actionbarCost = addonReader.ActionBarCostReader.Count;
                spellBook = addonReader.SpellBookReader.Count;

                // corresponding cells changes at every 5th update 
                for (int i = 0; i < 10; i++)
                {
                    wait.Update();
                }

            } while (actionbarCost != addonReader.ActionBarCostReader.Count ||
            spellBook != addonReader.SpellBookReader.Count);

            logger.LogInformation($"${nameof(addonReader.ActionBarCostReader)}: {actionbarCost} | ${nameof(addonReader.SpellBookReader)}: {spellBook}");
        }

    }
}
