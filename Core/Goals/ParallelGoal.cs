using Core.GOAP;

using Microsoft.Extensions.Logging;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Goals;

public sealed class ParallelGoal : GoapGoal
{
    public override float Cost => 3f;

    private readonly ILogger logger;
    private readonly ConfigurableInput input;
    private readonly StopMoving stopMoving;
    private readonly Wait wait;
    private readonly PlayerReader playerReader;
    private readonly BuffStatus<IPlayer> buffs;
    private readonly CastingHandler castingHandler;
    private readonly IMountHandler mountHandler;

    private static bool None() => false;
    private const int RecoverySettleFloorMs = 500;

    private bool castSuccess;
    private int recoveryCastRequested;

    public ParallelGoal(ILogger logger, ConfigurableInput input, Wait wait,
        PlayerReader playerReader, StopMoving stopMoving, ClassConfiguration classConfig,
        CastingHandler castingHandler, IMountHandler mountHandler, BuffStatus<IPlayer> buffs)
        : base(nameof(ParallelGoal))
    {
        this.logger = logger;
        this.input = input;
        this.stopMoving = stopMoving;
        this.wait = wait;
        this.playerReader = playerReader;
        this.buffs = buffs;
        this.castingHandler = castingHandler;
        this.mountHandler = mountHandler;

        AddPrecondition(GoapKey.incombat, false);

        Keys = classConfig.Parallel.Sequence;
    }

    public override bool CanRun()
    {
        for (int i = 0; i < Keys.Length; i++)
        {
            if (Keys[i].CanRun())
                return true;
        }
        return false;
    }

    public override void OnEnter()
    {
        if (mountHandler.IsMounted())
        {
            mountHandler.Dismount();
        }

        for (int i = 0; i < Keys.Length; i++)
        {
            if (Keys[i].BeforeCastStop)
            {
                stopMoving.Stop();
                wait.Update();
                break;
            }
        }
    }

    public override void Update()
    {
        if (castingHandler.SpellInQueue())
        {
            wait.Update();
            return;
        }

        if (!castSuccess)
        {
            Cast();

            wait.Update(playerReader.DoubleNetworkLatency);
            wait.Update();
        }
    }

    public override void OnExit()
    {
        castSuccess = false;
        wait.Update();
    }

    private void Cast()
    {
        recoveryCastRequested = 0;
        Parallel.For(0, Keys.Length, Execute);

        if (Interlocked.Exchange(ref recoveryCastRequested, 0) != 0)
        {
            stopMoving.Stop();
            wait.Fixed(Math.Max(RecoverySettleFloorMs, playerReader.DoubleNetworkLatency * 2));
            wait.Update();
        }
    }

    private void Execute(int i)
    {
        if (castingHandler.CastIfReady(Keys[i], None))
        {
            Keys[i].ResetCooldown();
            Keys[i].SetClicked();

            castSuccess = true;
            if (IsRecoveryAction(Keys[i]))
            {
                Interlocked.Exchange(ref recoveryCastRequested, 1);
            }
        }
    }

    private bool IsRecoveryAction(KeyAction action)
    {
        if (action.Name.Equals("Food", StringComparison.OrdinalIgnoreCase))
        {
            return !buffs.Food();
        }

        if (action.Name.Equals("Drink", StringComparison.OrdinalIgnoreCase))
        {
            return !buffs.Drink();
        }

        return false;
    }
}
