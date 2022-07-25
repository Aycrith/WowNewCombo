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
            int bag;

            do
            {
                actionbarCost = addonReader.ActionBarCostReader.Count;
                spellBook = addonReader.SpellBookReader.Count;
                bag = addonReader.BagReader.BagItems.Count;

                wait.Fixed(3000);

                logger.LogInformation($"{nameof(addonReader.ActionBarCostReader)}: {actionbarCost} | {nameof(addonReader.SpellBookReader)}: {spellBook} | {nameof(addonReader.BagReader)}: {addonReader.BagReader.BagItems.Count}");
            } while (
                actionbarCost != addonReader.ActionBarCostReader.Count ||
                spellBook != addonReader.SpellBookReader.Count ||
                bag != addonReader.BagReader.BagItems.Count);
        }

    }
}
