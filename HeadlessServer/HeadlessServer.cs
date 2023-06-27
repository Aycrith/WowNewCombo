using CommandLine;
using Core;
using Microsoft.Extensions.Logging;

namespace HeadlessServer;

public sealed partial class HeadlessServer
{
    private readonly ILogger<HeadlessServer> logger;
    private readonly IBotController botController;
    private readonly IAddonReader addonReader;
    private readonly ExecGameCommand exec;
    private readonly AddonConfigurator addonConfigurator;
    private readonly Wait wait;

    public HeadlessServer(ILogger<HeadlessServer> logger,
        IBotController botController,
        IAddonReader addonReader, ExecGameCommand exec,
        AddonConfigurator addonConfigurator, Wait wait)
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
        logger.LogInformation($"Running!");

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

            wait.Fixed(1000);

            LogInitStateStatus(logger, actionbarCost, spellBook, bag);
        } while (
            actionbarCost != addonReader.ActionBarCostReader.Count ||
            spellBook != addonReader.SpellBookReader.Count ||
            bag != addonReader.BagReader.BagItems.Count);
    }

    #region Logging

    [LoggerMessage(
        EventId = 4000,
        Level = LogLevel.Information,
        Message = "Actionbar: {actionbar,3} | SpellBook: {spellBook,3} | Bag: {bag,3}")]
    static partial void LogInitStateStatus(ILogger logger, int actionbar, int spellbook, int bag);

    #endregion

}
