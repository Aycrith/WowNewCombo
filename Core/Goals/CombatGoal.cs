using Core.GOAP;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
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
    private readonly GoapAgentState goapAgentState;

    private float lastDirection;
    private float lastMinDistance;
    private float lastMaxDistance;

    public CombatGoal(ILogger<CombatGoal> logger, ConfigurableInput input,
        Wait wait, PlayerReader playerReader, StopMoving stopMoving, AddonBits bits,
        ClassConfiguration classConfiguration, ClassConfiguration classConfig,
        CastingHandler castingHandler, CombatLog combatLog, GoapAgentState state,
        IMountHandler mountHandler)
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

        this.goapAgentState = state;

        AddPrecondition(GoapKey.incombat, true);
        AddPrecondition(GoapKey.hastarget, true);
        AddPrecondition(GoapKey.targetisalive, true);
        AddPrecondition(GoapKey.targethostile, true);
        //AddPrecondition(GoapKey.targettargetsus, true);
        AddPrecondition(GoapKey.incombatrange, true);

        AddEffect(GoapKey.producedcorpse, true);
        AddEffect(GoapKey.targetisalive, false);
        AddEffect(GoapKey.hastarget, false);

        Keys = classConfiguration.Combat.Sequence;
    }

    public void OnGoapEvent(GoapEventArgs e)
    {
        if (e is GoapStateEvent s && s.Key == GoapKey.producedcorpse)
        {
            // have to check range
            // ex. target died far away have to consider the range and approximate
            float distance = (lastMaxDistance + lastMinDistance) / 2f;
            SendGoapEvent(new CorpseEvent(GetCorpseLocation(distance), distance));
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

        if (bits.Target())
        {
            if (classConfig.AutoPetAttack &&
                bits.Pet() &&
                (!playerReader.PetTarget() || playerReader.PetTargetGuid != playerReader.TargetGuid) &&
                input.PetAttack.GetRemainingCooldown() == 0)
            {
                input.PressPetAttack();
            }

            ReadOnlySpan<KeyAction> span = Keys;
            for (int i = 0; bits.Target_Alive() && i < span.Length; i++)
            {
                KeyAction keyAction = span[i];

                if (castingHandler.SpellInQueue() && !keyAction.BaseAction)
                {
                    continue;
                }

                if (castingHandler.CastIfReady(keyAction,
                    keyAction.Interrupts.Count > 0
                    ? keyAction.CanBeInterrupted
                    : bits.Target_Alive))
                {
                    break;
                }
            }
        }

        if (!bits.Target())
        {
            logger.LogInformation("Lost target!");

            if (combatLog.DamageTakenCount() > 0)
            {
                stopMoving.Stop();
                FindNewTarget();
            }
            else
            {
                // dont save too close points
                logger.LogInformation("Target(s) Dead -- trying saving safe pos " + playerReader.MapPosNoZ.ToString());
                bool foundClosePoint = false;
                if (this.goapAgentState.safeLocations == null)
                {
                    return;
                }
                for (LinkedListNode<Vector3> point = this.goapAgentState.safeLocations.Last; point != null; point = point.Previous)
                {
                    Vector2 p1 = new Vector2(point.Value.X, point.Value.Y);
                    Vector2 p2 = new Vector2(playerReader.MapPos.X, playerReader.MapPos.Y);
                    if (Vector2.Distance(p1, p2) <= 0.1)
                    {
                        foundClosePoint = true;
                        break;
                    }
                }
                if (!foundClosePoint)
                {
                    Console.WriteLine("Saving safepos: " + playerReader.MapPosNoZ.ToString());
                    this.goapAgentState.safeLocations.AddLast(playerReader.MapPosNoZ);
                    if (this.goapAgentState.safeLocations.Count > 100)
                    {
                        this.goapAgentState.safeLocations.RemoveFirst();
                    }
                }
                input.PressClearTarget();
            }
        }
    }

    private void FindNewTarget()
    {
        if (playerReader.PetTarget() && combatLog.DeadGuid.Value != playerReader.PetTargetGuid)
        {
            ResetCooldowns();

            input.PressTargetPet();
            input.PressTargetOfTarget();
            wait.Update();

            if (!bits.Target_Dead())
            {
                logger.LogWarning("---- New targe from Pet target!");
                return;
            }

            input.PressClearTarget();
        }

        if (combatLog.DamageTakenCount() > 1)
        {
            logger.LogInformation("Checking target in front...");
            input.PressNearestTarget();
            wait.Update();

            if (bits.Target() && !bits.Target_Dead())
            {
                if (bits.Target_Combat() && bits.TargetTarget_PlayerOrPet())
                {
                    stopMoving.Stop();
                    ResetCooldowns();

                    logger.LogWarning("Found new target!");
                    return;
                }

                input.PressClearTarget();
                wait.Update();
            }
            else if (combatLog.DamageTakenCount() > 0)
            {
                logger.LogWarning($"---- Possible threats from behind {combatLog.DamageTakenCount()}. Waiting target by damage taken!");
                wait.Till(2500, bits.Target);
            }
        }
    }

    private Vector3 GetCorpseLocation(float distance)
    {
        return PointEstimator.GetPoint(playerReader.MapPos, playerReader.Direction, distance);
    }
}
