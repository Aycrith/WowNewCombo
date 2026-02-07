using Core.CombatRotation;
using Core.GOAP;

using Game;

using Microsoft.Extensions.Logging;

using System;
using System.Numerics;

namespace Core.Goals;

public sealed class CombatGoal : GoapGoal, IGoapEventListener
{
    public override float Cost => 4f;

    private readonly ILogger<CombatGoal> logger;
    private readonly ConfigurableInput input;
    private readonly ClassConfiguration classConfig;
    private readonly Wait wait;
    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;
    private readonly StopMoving stopMoving;
    private readonly CastingHandler castingHandler;
    private readonly IMountHandler mountHandler;
    private readonly CombatLog combatLog;
    private readonly IRotationOptimizer rotationOptimizer;

    private float lastDirection;
    private float lastMinDistance;
    private float lastMaxDistance;

    public CombatGoal(ILogger<CombatGoal> logger, ConfigurableInput input,
        Wait wait, PlayerReader playerReader, StopMoving stopMoving, AddonBits bits,
        ClassConfiguration classConfig,
        CastingHandler castingHandler, CombatLog combatLog,
        IMountHandler mountHandler,
        IRotationOptimizer rotationOptimizer)
        : base(nameof(CombatGoal))
    {
        this.logger = logger;
        this.input = input;

        this.wait = wait;
        this.playerReader = playerReader;
        this.bits = bits;
        this.combatLog = combatLog;

        this.stopMoving = stopMoving;
        this.castingHandler = castingHandler;
        this.mountHandler = mountHandler;
        this.classConfig = classConfig;
        this.rotationOptimizer = rotationOptimizer;

        AddPrecondition(GoapKey.incombat, true);
        AddPrecondition(GoapKey.hastarget, true);
        AddPrecondition(GoapKey.targetisalive, true);
        AddPrecondition(GoapKey.targethostile, true);
        //AddPrecondition(GoapKey.targettargetsus, true);
        AddPrecondition(GoapKey.incombatrange, true);

        AddEffect(GoapKey.producedcorpse, true);
        AddEffect(GoapKey.targetisalive, false);
        AddEffect(GoapKey.hastarget, false);

        Keys = classConfig.Combat.Sequence;
    }

    public void OnGoapEvent(GoapEventArgs e)
    {
        if (e is GoapStateEvent s && s.Key == GoapKey.producedcorpse)
        {
            // have to check range
            // ex. target died far away have to consider the range and approximate
            float distance = (lastMaxDistance + lastMinDistance) / 2f;
            SendGoapEvent(new CorpseEvent(GetCorpseLocation(distance), distance, playerReader.Direction, playerReader.MapPos));
        }
    }

    private void ResetCooldowns()
    {
        ReadOnlySpan<KeyAction> span = Keys;
        for (int i = 0; i < span.Length; i++)
        {
            KeyAction keyAction = span[i];
            if (keyAction.ResetOnNewTarget)
            {
                keyAction.ResetCooldown();
                keyAction.ResetCharges();
            }
        }
    }

    public override void OnEnter()
    {
        if (mountHandler.IsMounted())
        {
            mountHandler.Dismount();
        }

        lastDirection = playerReader.Direction;
    }

    public override void OnExit()
    {
        if (combatLog.DamageTakenCount() > 0 && !bits.Target())
        {
            stopMoving.Stop();
        }
    }

    public override void Update()
    {
        wait.Update();

        if (MathF.Abs(lastDirection - playerReader.Direction) > MathF.PI / 2)
        {
            logger.LogInformation("Turning too fast!");
            stopMoving.Stop();
        }

        lastDirection = playerReader.Direction;
        lastMinDistance = playerReader.MinRange();
        lastMaxDistance = playerReader.MaxRange();

        if (bits.Drowning())
        {
            input.PressJump();
            return;
        }

        if (classConfig.AutoPetAttack &&
            bits.Pet() &&
            (!playerReader.PetTarget() || playerReader.PetTargetGuid != playerReader.TargetGuid) &&
            !input.PetAttack.OnCooldown())
        {
            input.PressPetAttack();
        }

        ReadOnlySpan<KeyAction> span = Keys;

        // Combat Rotation Optimizer: if enabled, score and sort abilities
        if (rotationOptimizer.IsEnabled && span.Length > 0 && span.Length <= 32)
        {
            GameStateSnapshot state = new(
                HealthPercent: playerReader.HealthPercent(),
                ResourcePercent: playerReader.PTPercentage(),
                ResourceCurrent: playerReader.PTCurrent(),
                ResourceMax: playerReader.PTMax(),
                ComboPoints: playerReader.ComboPoints(),
                TargetHealthPercent: playerReader.TargetHealthPercent(),
                GcdRemainingMs: playerReader.GCD.Value,
                NetworkLatencyMs: playerReader.NetworkLatency,
                SpellQueueMs: playerReader.SpellQueueTimeMs,
                MainHandSwingElapsedMs: playerReader.MainHandSwing.ElapsedMs(),
                MainHandSpeedMs: playerReader.MainHandSpeedMs(),
                MobCount: GetMobCount(),
                InCombat: bits.Combat(),
                TargetAlive: bits.Target_Alive(),
                IsTargetCasting: playerReader.IsTargetCasting(),
                TickTimestamp: Environment.TickCount64);

            Span<int> sortedIndices = stackalloc int[span.Length];
            int count = rotationOptimizer.Optimize(span, in state, sortedIndices);

            for (int i = 0; bits.Target_Alive() && i < count; i++)
            {
                KeyAction keyAction = span[sortedIndices[i]];

                if (castingHandler.SpellInQueue() && !keyAction.BaseAction)
                    continue;

                bool interrupt() => bits.Target_Alive() && keyAction.CanBeInterrupted();

                if (castingHandler.CastIfReady(keyAction, interrupt))
                {
                    rotationOptimizer.RecordCastResult(keyAction, true);
                    break;
                }
                else
                {
                    rotationOptimizer.RecordCastResult(keyAction, false);
                }
            }
        }
        else
        {
            // Original static priority path (zero overhead when optimizer disabled)
            for (int i = 0; bits.Target_Alive() && i < span.Length; i++)
            {
                KeyAction keyAction = span[i];

                if (castingHandler.SpellInQueue() && !keyAction.BaseAction)
                {
                    continue;
                }

                bool interrupt() => bits.Target_Alive() && keyAction.CanBeInterrupted();

                if (castingHandler.CastIfReady(keyAction, interrupt))
                {
                    break;
                }
            }
        }

        if (bits.SoftInteract_Enabled())
        {
            DealWithSoftInteract();
        }

        if (!bits.Target() || (bits.Target() && bits.Target_Dead()))
        {
            logger.LogInformation("Lost target!");

            if (combatLog.DamageTakenCount() > 0)
            {
                if (bits.Target() && bits.Target_Dead())
                {
                    logger.LogInformation("Clear current dead target!");
                    input.ForceAggressiveClearTarget(wait, bits);
                }

                logger.LogWarning("Search Possible Threats!");
                stopMoving.Stop();

                FindPossibleThreats();
            }
            else
            {
                input.ForceAggressiveClearTarget(wait, bits);
            }
        }
    }

    /// <summary>
    /// Gets the current number of mobs in combat with the player.
    /// Uses CombatLog.DamageTaken as a proxy for engaged enemies.
    /// </summary>
    private int GetMobCount()
    {
        // Count unique mobs that have damaged us (best available proxy)
        int mobCount = combatLog.DamageTakenCount();
        
        // If we have a target but no damage taken yet, count it
        if (mobCount == 0 && bits.Target_Alive())
        {
            mobCount = 1;
        }
        
        return mobCount;
    }

    private void FindPossibleThreats()
    {
        if (bits.Pet_Defensive())
        {
            float elapsedPetFoundTarget = wait.Until(CastingHandler.GCD,
                () => playerReader.PetTarget() && bits.PetTarget_Alive());

            if (elapsedPetFoundTarget < 0)
            {
                logger.LogWarning("Pet not found target!");
                input.ForceAggressiveClearTarget(wait, bits);
                return;
            }

            ResetCooldowns();

            input.PressTargetPet();
            input.PressTargetOfTarget();
            wait.Update();

            logger.LogWarning($"Found new target by pet. {elapsedPetFoundTarget}ms");

            return;
        }

        logger.LogInformation("Checking target in front...");
        input.PressNearestTarget();
        wait.Update();

        if (bits.Target() && !bits.Target_Dead() && bits.Target_Hostile())
        {
            if (bits.Target_Combat() && bits.TargetTarget_PlayerOrPet())
            {
                ResetCooldowns();

                logger.LogWarning("Found new target!");
                wait.Update();
                return;
            }

            logger.LogWarning("Dont pull non-hostile target!");
            input.ForceAggressiveClearTarget(wait, bits);
        }

        logger.LogWarning($"Waiting for target to exists or lose combat. Possible threats {combatLog.DamageTakenCount()}!");
        wait.Till(CastingHandler.GCD * 2,
            () => bits.Target_Alive() || !bits.Combat());
    }

    private Vector3 GetCorpseLocation(float distance)
    {
        return PointEstimator.GetMapPos(playerReader.WorldMapArea, playerReader.WorldPos, playerReader.Direction, distance);
    }

    private void DealWithSoftInteract()
    {
        if (!playerReader.IsInMeleeRange() ||
            playerReader.IsCasting() ||
            !InvalidSoftInteractExists() ||
            playerReader.TargetGuid == playerReader.SoftInteract_Guid)
        {
            return;
        }

        // TODO: have to find a better way to deal with this
        // Dead code removed - feature intentionally disabled pending redesign
    }

    private bool InvalidSoftInteractExists()
    {
        return
            bits.SoftInteract() &&
            (
            playerReader.SoftInteract_Type != GuidType.Creature ||
            bits.SoftInteract_Dead() ||
            bits.SoftInteract_Tagged()
            );
    }
}
