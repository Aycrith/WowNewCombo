using Core.Database;
using Core.GOAP;
using SharedLib.NpcFinder;
using Microsoft.Extensions.Logging;
using WowheadDB;
using System.Collections.Generic;
using System.Numerics;
using SharedLib.Extensions;
using System;
using System.Threading;
using Core.AddonComponent;

namespace Core.Goals;

public sealed partial class LootGoal : GoapGoal, IGoapEventListener
{
    public override float Cost => 4.6f;

    private const int MAX_TIME_TO_REACH_MELEE = 10000;
    private const int MAX_TIME_TO_DETECT_LOOT = 2 * CastingHandler.GCD;
    private const int MAX_TIME_TO_RESET_LOOT = 800;

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
    private readonly CombatLog combatLog;
    private readonly PlayerDirection playerDirection;
    private readonly GoapAgentState state;

    private readonly CancellationToken token;

    private readonly List<CorpseEvent> corpseLocations = [];

    private bool canGather;
    private int targetId;
    private int bagHashNewOrStackGain;
    private int money;

    public LootGoal(ILogger<LootGoal> logger,
        ConfigurableInput input, Wait wait,
        PlayerReader playerReader, AreaDB areaDb, BagReader bagReader,
        StopMoving stopMoving, AddonBits bits,
        ClassConfiguration classConfig, NpcNameTargeting npcNameTargeting,
        PlayerDirection playerDirection,
        GoapAgentState state, CombatLog combatLog,
        CancellationTokenSource cts)
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
        this.playerDirection = playerDirection;
        this.state = state;

        this.token = cts.Token;
        AddPrecondition(GoapKey.pulled, false);
        AddPrecondition(GoapKey.incombat, false);
        AddPrecondition(GoapKey.dangercombat, false);
        AddPrecondition(GoapKey.shouldloot, true);
        AddEffect(GoapKey.shouldloot, false);
    }

    public override void OnEnter()
    {
        WaitForLootReset();

        if (combatLog.DamageTakenCount() == 0)
        {
            WaitForLosingTarget();
        }

        CaptureStateBeforeLoot();

        CheckInventoryFull();

        if (TryLoot())
        {
            HandleSuccessfulLoot();
        }
        else
        {
            HandleFailedLoot();
        }

        CleanUpAfterLooting();

        ClearTargetIfNeeded();
    }

    private void WaitForLootReset()
    {
        wait.WhileWithTimeout(LootReset, MAX_TIME_TO_RESET_LOOT);
    }

    private void WaitForLosingTarget()
    {
        float elapsedMs = wait.Until(
            Loot.LOOTFRAME_AUTOLOOT_DELAY, bits.NoTarget);

        LogLostTarget(logger, elapsedMs);
    }

    private void CaptureStateBeforeLoot()
    {
        bagHashNewOrStackGain = bagReader.HashNewOrStackGain;
        money = playerReader.Money;
    }

    private void CheckInventoryFull()
    {
        ClearTargetIfNeeded();
        if (!bagReader.BagsFull())
            return;

        logger.LogWarning("Inventory is full");
    }

    private bool ShouldTryKeyboardLoot()
    {
        return input.KeyboardOnly || state.LastCombatKillCount == 1;
    }

    private bool TryLoot()
    {
        if (ShouldTryKeyboardLoot())
        {
            bool success = LootKeyboard();
            if (!success && state.LastCombatKillCount == 1)
            {
                LogKeyboardLootFailed(logger, bits.Target());
            }

            if (success)
            {
                return true;
            }
        }
        if (!input.KeyboardOnly) {
            return LootMouse();
        }
        return false;
    }

    private void HandleSuccessfulLoot()
    {
        float elapsedMs = wait.Until(MAX_TIME_TO_DETECT_LOOT,
            LootWindowClosedOrMoneyChanged,
            input.PressApproachOnCooldown);

        bool success = elapsedMs >= 0;
        if (success && !bagReader.BagsFull())
        {
            LogLootSuccess(logger, elapsedMs);
        }
        else
        {
            SendGoapEvent(ScreenCaptureEvent.Default);
            LogLootFailed(logger, elapsedMs);
        }

        if (success)
        {
            GatherCorpseIfNeeded();
        }
    }

    private void GatherCorpseIfNeeded()
    {
        if (!canGather)
            return;

        state.GatherableCorpseCount++;

        CorpseEvent? ce = GetClosestCorpse();
        if (ce == null)
            return;

        SendGoapEvent(new SkinCorpseEvent(ce.MapLoc, ce.Radius, targetId));
    }

    private void HandleFailedLoot()
    {
        SendGoapEvent(ScreenCaptureEvent.Default);
        Log("Loot Failed, target not found!");
    }

    private void CleanUpAfterLooting()
    {
        SendGoapEvent(new RemoveClosestPoi(CorpseEvent.NAME));
        state.LootableCorpseCount = Math.Max(0, state.LootableCorpseCount - 1);

        if (corpseLocations.Count > 0)
        {
            corpseLocations.Remove(GetClosestCorpse()!);
        }
    }

    private void ClearTargetIfNeeded()
    {
        if (canGather || !bits.Target())
        {
            return;
        }

        input.PressClearTarget();
        wait.Update();

        if (bits.Target())
        {
            SendGoapEvent(ScreenCaptureEvent.Default);
            LogWarning("Unable to clear target! Check Bindpad settings!");
        }
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

        ReadOnlySpan<CursorType> types = [CursorType.Loot];
        if (!npcNameTargeting.FindBy(types, token))
        {
            return false;
        }

        Log("Nearest Corpse clicked...");
        float elapsedMs = wait.Until(playerReader.NetworkLatency, bits.Target);
        LogFoundNpcNameCount(logger, npcNameTargeting.NpcCount, elapsedMs);

        npcNameTargeting.ChangeNpcType(NpcNames.None);

        CheckForCanGather();

        return playerReader.MinRangeZero() || MoveToTargetAndReached();
    }

    private CorpseEvent? GetClosestCorpse()
    {
        CorpseEvent? closest = null;

        float minDistance = float.MaxValue;
        Vector3 playerMapLoc = playerReader.MapPos;

        foreach (CorpseEvent corpse in corpseLocations)
        {
            float distance = playerMapLoc.MapDistanceXYTo(corpse.MapLoc);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = corpse;
            }
        }

        return closest;
    }

    private void CheckForCanGather()
    {
        if (!classConfig.GatherCorpse ||
            areaDb.CurrentArea == null)
            return;

        targetId = playerReader.TargetId;
        Area area = areaDb.CurrentArea;

        canGather = GatherAvailable(classConfig, area, targetId);

        LogShouldGather(logger, targetId, canGather);
    }

    private static bool GatherAvailable(ClassConfiguration config, Area area, int npcId) =>
        (config.Skin && area.skinnable.AsSpan().BinarySearch(npcId) >= 0) ||
        (config.Herb && area.gatherable.AsSpan().BinarySearch(npcId) >= 0) ||
        (config.Mine && area.minable.AsSpan().BinarySearch(npcId) >= 0) ||
        (config.Salvage && area.salvegable.AsSpan().BinarySearch(npcId) >= 0);

    private bool LootWindowClosedOrMoneyChanged()
    {
        // hack: warlock soul shard mechanic marks loot success prematurely
        return //bagHashNewOrStackGain != bagReader.HashNewOrStackGain ||
            money != playerReader.Money ||
            (LootStatus)playerReader.LootEvent.Value is LootStatus.CLOSED;
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
            input.PressLastTarget();
            wait.Update();

            if (bits.Target())
                Log($"Keyboard last target found!");
        }

        if (EligibleSoftTargetExists())
        {
            Log($"Keyboard soft target found!");

            input.PressInteract();
            wait.Update();
        }

        if (!bits.Target())
        {
            LogWarning($"Keyboard No target found!");
            return false;
        }

        if (!bits.Target_Dead())
        {
            LogWarning("Keyboard Don't attack alive target!");

            input.PressClearTarget();
            wait.Update();

            return false;
        }

        CheckForCanGather();

        input.PressFastInteract();
        wait.Update();

        return playerReader.MinRangeZero() || MoveToTargetAndReached();
    }

    private bool EligibleSoftTargetExists() =>
        //!bits.Target() &&
        bits.SoftInteract() &&
        bits.SoftInteract_Hostile() &&
        bits.SoftInteract_Dead() &&
        !bits.SoftInteract_Tagged() &&
        playerReader.SoftInteract_Type == GuidType.Creature;

    private bool MoveToTargetAndReached()
    {
        wait.While(input.Approach.OnCooldown);

        float elapsedMs = wait.Until(MAX_TIME_TO_REACH_MELEE,
            bits.NotMoving, input.PressApproachOnCooldown);

        LogReachedCorpse(logger, bits.Target(), elapsedMs);

        return bits.Target() && playerReader.MinRangeZero();
    }

    private bool LootReset() =>
        (LootStatus)playerReader.LootEvent.Value != LootStatus.CORPSE;

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
        Message = "Has target ? {hasTarget} | Reached corpse ? {elapsedMs}ms")]
    static partial void LogReachedCorpse(ILogger logger, bool hasTarget, float elapsedMs);

    [LoggerMessage(
        EventId = 0134,
        Level = LogLevel.Information,
        Message = "Should gather {targetId} ? {shouldGather}")]
    static partial void LogShouldGather(ILogger logger, int targetId, bool shouldGather);

    [LoggerMessage(
        EventId = 0135,
        Level = LogLevel.Information,
        Message = "Lost target {elapsedMs}ms")]
    static partial void LogLostTarget(ILogger logger, float elapsedMs);

    [LoggerMessage(
        EventId = 0136,
        Level = LogLevel.Error,
        Message = "Keyboard loot failed! Has target ? {hasTarget}")]
    static partial void LogKeyboardLootFailed(ILogger logger, bool hasTarget);

    #endregion
}
