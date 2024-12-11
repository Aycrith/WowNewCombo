using Core.GOAP;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Numerics;

namespace Core.Goals;

public sealed class FleeGoal : GoapGoal
{
    public override float Cost => 4f;
    private readonly ILogger<CombatGoal> logger;
    private readonly ConfigurableInput input;
    private readonly ClassConfiguration classConfig;
    private readonly Wait wait;
    private readonly PlayerReader playerReader;
    private readonly Navigation playerNavigation;
    private readonly AddonBits bits;
    private readonly StopMoving stopMoving;
    private readonly CastingHandler castingHandler;
    private readonly IMountHandler mountHandler;
    private readonly CombatLog combatLog;
    private readonly GoapAgentState goapAgentState;
    public int MOB_COUNT = 1;
    public bool runningAway;

    public FleeGoal(ILogger<CombatGoal> logger, ConfigurableInput input,
        Wait wait, PlayerReader playerReader, StopMoving stopMoving, AddonBits bits,
        ClassConfiguration classConfiguration, Navigation playerNavigation, GoapAgentState state,
        ClassConfiguration classConfig, CastingHandler castingHandler, CombatLog combatLog,
        IMountHandler mountHandler)
        : base(nameof(CombatGoal))
    {
        this.logger = logger;
        this.input = input;

        this.runningAway = false;
        this.wait = wait;
        this.playerReader = playerReader;
        this.playerNavigation = playerNavigation;
        this.bits = bits;
        this.combatLog = combatLog;

        this.stopMoving = stopMoving;
        this.castingHandler = castingHandler;
        this.mountHandler = mountHandler;
        this.classConfig = classConfig;
        this.goapAgentState = state;

        AddPrecondition(GoapKey.incombat, true);
        //AddPrecondition(GoapKey.hastarget, true);
        //AddPrecondition(GoapKey.targetisalive, true);
        //AddPrecondition(GoapKey.targethostile, true);
        //AddPrecondition(GoapKey.targettargetsus, true);
        //AddPrecondition(GoapKey.incombatrange, true);

        //AddEffect(GoapKey.producedcorpse, true);
        //AddEffect(GoapKey.targetisalive, false);
        //AddEffect(GoapKey.hastarget, false);

        Keys = classConfiguration.Combat.Sequence;
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

        this.runningAway = false;
        playerNavigation.Stop();
        playerNavigation.SetWayPoints(stackalloc Vector3[] { });
        playerNavigation.ResetStuckParameters();
    }

    public override void OnExit()
    {
        if (combatLog.DamageTakenCount() > 0 && !bits.Target())
        {
            stopMoving.Stop();
        }
        // clearing
        this.runningAway = false;
        playerNavigation.Stop();
        playerNavigation.SetWayPoints(stackalloc Vector3[] { });
        playerNavigation.ResetStuckParameters();
    }

    public override void Update()
    {
        wait.Update();
        if (this.goapAgentState.safeLocations.Count >= MOB_COUNT && this.runningAway == false)
        {

            bool foundPoint = false;
            logger.LogInformation("Flee Goal Activated. Current Pos: " + playerReader.MapPos.ToString() + ",Safe Spots: " + goapAgentState.safeLocations.Count);
            if (goapAgentState.safeLocations == null)
            {
                return;
            }
            for (LinkedListNode<Vector3> point = goapAgentState.safeLocations.Last; point != null; point = point.Previous)
            {
                Vector2 p1 = new Vector2(point.Value.X, point.Value.Y);
                Vector2 p2 = new Vector2(playerReader.MapPos.X, playerReader.MapPos.Y);
                if (Vector2.Distance(p1, p2) >= 2.2)
                {
                    // select the point far enough to lose the current mobs.
                    input.PressClearTarget();
                    playerNavigation.Stop();
                    playerNavigation.ResetStuckParameters();
                    playerNavigation.SetWayPoints(stackalloc Vector3[] { (Vector3)(point.Value) });
                    playerNavigation.Update();
                    Console.WriteLine("Found point " + point.Value.ToString());
                    foundPoint = true;
                    this.runningAway = true;
                    break;
                }
            }
            if (foundPoint)
            {
                logger.LogInformation("Running away to the last safe point!");
                return;
            }
            else
            {
                logger.LogInformation("Cant run away, figting!");
            }
        }
    }
}
