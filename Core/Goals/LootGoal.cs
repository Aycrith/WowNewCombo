using Core.Database;
using Core.GOAP;
using SharedLib.NpcFinder;
using Microsoft.Extensions.Logging;
using WowheadDB;
using System.Collections.Generic;
using System.Numerics;
using SharedLib.Extensions;
using System;

namespace Core.Goals;

public sealed partial class LootGoal : GoapGoal, IGoapEventListener
{
    public override float Cost => 4.6f;

    private const int MAX_TIME_TO_REACH_MELEE = 10000;
    private const int MAX_TIME_TO_DETECT_LOOT = 2 * CastingHandler.GCD;
    private const int MAX_TIME_TO_WAIT_NPC_NAME = 1000;

    private readonly ILogger<LootGoal> logger;
    private readonly ConfigurableInput input;

    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;
    private readonly Wait wait;
    private readonly AreaDB areaDb;
    private readonly StopMoving stopMoving;
    private readonly BagReader bagReader;
    private readonly ClassConfiguration classConfig;
    private readonly NpcNameTargeting npcNameTargeting;
    private readonly CombatUtil combatUtil;
    private readonly CombatLog combatLog;
    private readonly PlayerDirection playerDirection;
    private readonly GoapAgentState state;

    private readonly List<CorpseEvent> corpseLocations = new();

    private bool gatherCorpse;
    private int targetId;
    private int bagHashNewOrStackGain;
    private int money;

    public LootGoal(ILogger<LootGoal> logger, ConfigurableInput input, Wait wait,
        PlayerReader playerReader, AreaDB areaDb, BagReader bagReader,
        StopMoving stopMoving, AddonBits bits,
        ClassConfiguration classConfig, NpcNameTargeting npcNameTargeting,
        CombatUtil combatUtil, PlayerDirection playerDirection,
        GoapAgentState state, CombatLog combatLog)
        : base(nameof(LootGoal))
    {
        this.logger = logger;
        this.input = input;

        this.wait = wait;
        this.playerReader = playerReader;
        this.bits = bits;
        this.areaDb = areaDb;
        this.stopMoving = stopMoving;
        this.bagReader = bagReader;
        this.combatLog = combatLog;
        this.classConfig = classConfig;
        this.npcNameTargeting = npcNameTargeting;
        this.combatUtil = combatUtil;
        this.playerDirection = playerDirection;
        this.state = state;

        AddPrecondition(GoapKey.shouldloot, true);
        AddEffect(GoapKey.shouldloot, false);
    }

    public override void OnEnter()
    {
        combatUtil.Update();

        wait.While(LootReset);

        if (combatLog.DamageTakenCount() == 0)
        {
            float eMs = wait.Until(Loot.LOOTFRAME_AUTOLOOT_DELAY, bits.NoTarget);
            LogLostTarget(logger, eMs);
        }

        bagHashNewOrStackGain = bagReader.HashNewOrStackGain;
        money = playerReader.Money;

        if (bagReader.BagsFull())
        {
            logger.LogWarning("Inventory is full");
        }

        bool success = false;
        if (input.KeyboardOnly)
        {
            success = LootKeyboard();
        }
        else
        {
            if (state.LastCombatKillCount == 1)
            {
                success = LootKeyboard();
            }

            if (!success)
            {
                success = LootMouse();
            }
        }

        if (success)
        {
            float e = wait.Until(MAX_TIME_TO_DETECT_LOOT, LootWindowClosedOrBagOrMoneyChanged, input.PressApproachOnCooldown);
            success = e >= 0;
            if (success && !bagReader.BagsFull())
            {
                LogLootSuccess(logger, e);
            }
            else
            {
                SendGoapEvent(ScreenCaptureEvent.Default);
                LogLootFailed(logger, e);
            }

            gatherCorpse &= success;

            if (gatherCorpse)
            {
                state.GatherableCorpseCount++;

                CorpseEvent? ce = GetClosestCorpse();
                if (ce != null)
                    SendGoapEvent(new SkinCorpseEvent(ce.MapLoc, ce.Radius, targetId));
            }
        }
        else
        {
            SendGoapEvent(ScreenCaptureEvent.Default);
            Log($"Loot Failed, target not found!");
        }

        SendGoapEvent(new RemoveClosestPoi(CorpseEvent.NAME));
        state.LootableCorpseCount = Math.Max(0, state.LootableCorpseCount - 1);

        if (!gatherCorpse && bits.Target())
        {
            input.PressClearTarget();
            wait.Update();

            if (bits.Target())
            {
                SendGoapEvent(ScreenCaptureEvent.Default);
                LogWarning("Unable to clear target! Check Bindpad settings!");
            }
        }

        if (corpseLocations.Count > 0)
            corpseLocations.Remove(GetClosestCorpse()!);
    }

    public void OnGoapEvent(GoapEventArgs e)
    {
        if (e is CorpseEvent corpseEvent)
        {
            corpseLocations.Add(corpseEvent);
        }
    }

    private bool FoundByCursor()
    {
        npcNameTargeting.ChangeNpcType(NpcNames.Corpse);

        wait.Fixed(playerReader.NetworkLatency);
        npcNameTargeting.WaitForUpdate();

        ReadOnlySpan<CursorType> types = stackalloc[] { CursorType.Loot };
        if (!npcNameTargeting.FindBy(types))
        {
            return false;
        }

        Log("Nearest Corpse clicked...");
        float elapsedMs = wait.Until(playerReader.NetworkLatency, bits.Target);
        LogFoundNpcNameCount(logger, npcNameTargeting.NpcCount, elapsedMs);

        npcNameTargeting.ChangeNpcType(NpcNames.None);

        CheckForGather();

        if (!playerReader.MinRangeZero())
        {
            elapsedMs =
                wait.AfterEquals(MAX_TIME_TO_REACH_MELEE, 2, playerReader._MapPosNoZ,
                input.PressApproachOnCooldown);
            LogReachedCorpse(logger, elapsedMs);

            return playerReader.MinRangeZero();
        }

        return true;
    }

    private CorpseEvent? GetClosestCorpse()
    {
        if (corpseLocations.Count == 0)
            return null;

        int index = -1;
        float minDistance = float.MaxValue;
        Vector3 playerMap = playerReader.MapPos;
        for (int i = 0; i < corpseLocations.Count; i++)
        {
            float mapDist = playerMap.MapDistanceXYTo(corpseLocations[i].MapLoc);
            if (mapDist < minDistance)
            {
                minDistance = mapDist;
                index = i;
            }
        }

        return corpseLocations[index];
    }

    private void CheckForGather()
    {
        if (!classConfig.GatherCorpse ||
            areaDb.CurrentArea == null)
            return;

        gatherCorpse = false;
        targetId = playerReader.TargetId;
        Area area = areaDb.CurrentArea;

        if ((classConfig.Skin && Array.BinarySearch(area.skinnable, targetId) >= 0) ||
           (classConfig.Herb && Array.BinarySearch(area.gatherable, targetId) >= 0) ||
           (classConfig.Mine && Array.BinarySearch(area.minable, targetId) >= 0) ||
           (classConfig.Salvage && Array.BinarySearch(area.salvegable, targetId) >= 0))
        {
            gatherCorpse = true;
        }

        LogShouldGather(logger, targetId, gatherCorpse);
    }

    private bool LootWindowClosedOrBagOrMoneyChanged()
    {
        // hack: warlock soul shard mechanic marks loot success prematurely
        return //bagHashNewOrStackGain != bagReader.HashNewOrStackGain ||
            money != playerReader.Money ||
            (LootStatus)playerReader.LootEvent.Value is
            LootStatus.CLOSED;
    }

    private bool LootMouse()
    {
        stopMoving.Stop();
        wait.Update();

        if (FoundByCursor())
        {
            return true;
        }
        else if (corpseLocations.Count > 0)
        {
            Vector3 playerMap = playerReader.MapPos;
            CorpseEvent e = GetClosestCorpse()!;
            float heading = DirectionCalculator.CalculateMapHeading(playerMap, e.MapLoc);
            playerDirection.SetDirection(heading, e.MapLoc);

            logger.LogInformation("Look at possible closest corpse and try once again...");

            wait.Fixed(playerReader.NetworkLatency);

            if (FoundByCursor())
            {
                return true;
            }
        }

        return LootKeyboard();
    }

    private bool LootKeyboard()
    {
        if (!bits.Target())
        {
            input.PressFastLastTarget();
            wait.Update();

            if (!bits.Target())
                return false;
        }

        if (!bits.Target_Dead())
        {
            LogWarning("Don't attack alive target!");
            input.PressClearTarget();
            wait.Update();

            return false;
        }

        CheckForGather();

        Log("Target Last Target Found!");
        input.PressFastInteract();
        wait.Update();

        if (!playerReader.MinRangeZero())
        {
            float elapsedMs =
                wait.AfterEquals(MAX_TIME_TO_REACH_MELEE, 2, playerReader._MapPosNoZ,
                input.PressApproachOnCooldown);
            LogReachedCorpse(logger, elapsedMs);

            return playerReader.MinRangeZero();
        }

        return true;
    }

    private bool LootReset()
    {
        return (LootStatus)playerReader.LootEvent.Value != LootStatus.CORPSE;
    }

    #region Logging

    private void Log(string text)
    {
        logger.LogInformation(text);
    }

    private void LogWarning(string text)
    {
        logger.LogWarning(text);
    }

    [LoggerMessage(
        EventId = 0130,
        Level = LogLevel.Information,
        Message = "Loot Successful {elapsedMs}ms")]
    static partial void LogLootSuccess(ILogger logger, float elapsedMs);

    [LoggerMessage(
        EventId = 0131,
        Level = LogLevel.Information,
        Message = "Loot Failed {elapsedMs}ms")]
    static partial void LogLootFailed(ILogger logger, float elapsedMs);

    [LoggerMessage(
        EventId = 0132,
        Level = LogLevel.Information,
        Message = "Found NpcName Count: {npcCount} {elapsedMs}ms")]
    static partial void LogFoundNpcNameCount(ILogger logger, int npcCount, float elapsedMs);

    [LoggerMessage(
        EventId = 0133,
        Level = LogLevel.Information,
        Message = "Reached corpse ? {elapsedMs}ms")]
    static partial void LogReachedCorpse(ILogger logger, float elapsedMs);

    [LoggerMessage(
        EventId = 0134,
        Level = LogLevel.Information,
        Message = "Should gather {targetId} ? {gatherCorpse}")]
    static partial void LogShouldGather(ILogger logger, int targetId, bool gatherCorpse);

    [LoggerMessage(
        EventId = 0135,
        Level = LogLevel.Information,
        Message = "Lost target {elapsedMs}ms")]
    static partial void LogLostTarget(ILogger logger, float elapsedMs);

    #endregion
}