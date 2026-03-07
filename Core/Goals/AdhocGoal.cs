using Core.GOAP;

using Microsoft.Extensions.Logging;
using System;

namespace Core.Goals;

public sealed class AdhocGoal : GoapGoal
{
    public override float Cost => key.Cost;

    private readonly ILogger logger;
    private readonly ConfigurableInput input;

    private readonly Wait wait;
    private readonly StopMoving stopMoving;
    private readonly PlayerReader playerReader;

    private readonly KeyAction key;
    private readonly CastingHandler castingHandler;
    private readonly IMountHandler mountHandler;
    private readonly AddonBits bits;
    private readonly CombatLog combatLog;
    private readonly BagReader bagReader;
    private readonly ExecGameCommand execGameCommand;

    private readonly bool? combatMatters;
    private static readonly int[] HealthstoneItemIds =
    [
        34062, 30703, 22044,
        22105, 22104, 22103,
        19013, 19012, 9421,
        19011, 19010, 5510,
        19009, 19008, 5509,
        19007, 19006, 5511,
        19005, 19004, 5512
    ];

    public AdhocGoal(KeyAction key, ILogger logger,
        ConfigurableInput input, Wait wait,
        PlayerReader playerReader, StopMoving stopMoving,
        CastingHandler castingHandler, IMountHandler mountHandler,
        AddonBits bits, CombatLog combatLog,
        BagReader bagReader, ExecGameCommand execGameCommand)
        : base(nameof(AdhocGoal))
    {
        this.logger = logger;
        this.input = input;
        this.wait = wait;
        this.stopMoving = stopMoving;
        this.playerReader = playerReader;
        this.key = key;
        this.castingHandler = castingHandler;
        this.mountHandler = mountHandler;
        this.bits = bits;
        this.combatLog = combatLog;
        this.bagReader = bagReader;
        this.execGameCommand = execGameCommand;

        if (bool.TryParse(key.InCombat, out bool result))
        {
            AddPrecondition(GoapKey.incombat, result);
            combatMatters = result;
        }

        Keys = [key];
    }

    public override bool CanRun() => key.CanRun();

    public override void OnEnter()
    {
        if (key.BeforeCastDismount && mountHandler.IsMounted())
        {
            mountHandler.Dismount();
        }
    }

    public override void Update()
    {
        wait.Update();

        if (!CanRun() || castingHandler.SpellInQueue())
            return;

        if (TryRunCommandDrivenAction())
            return;

        if (key.Charge >= 1 && key.CanRun())
            castingHandler.CastIfReady(key, Interrupt);
    }

    private bool TryRunCommandDrivenAction()
    {
        if (key.Name.Equals("Create Healthstone", StringComparison.OrdinalIgnoreCase))
        {
            if (playerReader.IsCasting())
            {
                return true;
            }

            execGameCommand.Run("/cast Create Healthstone");
            key.SetClicked();
            wait.Update();
            logger.LogInformation("[AdhocGoal        ] Create Healthstone requested via command cast.");
            return true;
        }

        if (key.Name.Equals("Use Healthstone", StringComparison.OrdinalIgnoreCase) ||
            key.Name.Equals("Healthstone", StringComparison.OrdinalIgnoreCase))
        {
            int itemId = TryGetAvailableHealthstoneItemId();
            if (itemId <= 0)
            {
                return false;
            }

            execGameCommand.Run($"/use item:{itemId}");
            key.SetClicked();
            wait.Update();
            logger.LogInformation("[AdhocGoal        ] Healthstone use requested for item:{ItemId}.", itemId);
            return true;
        }

        return false;
    }

    private int TryGetAvailableHealthstoneItemId()
    {
        ReadOnlySpan<int> ids = HealthstoneItemIds;
        for (int i = 0; i < ids.Length; i++)
        {
            if (bagReader.ItemCount(ids[i]) > 0)
            {
                return ids[i];
            }
        }

        return 0;
    }

    private bool Interrupt()
    {
        return combatMatters.HasValue
            ? combatMatters.Value == bits.Combat() && combatLog.DamageTakenCount() > 0
            : combatLog.DamageTakenCount() > 0;
    }
}
