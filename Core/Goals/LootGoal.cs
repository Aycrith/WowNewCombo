using Core.Database;
using Core.GOAP;

using Microsoft.Extensions.Logging;

using SharedLib.Extensions;
using SharedLib.NpcFinder;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

using WowheadDB;

namespace Core.Goals;

public sealed partial class LootGoal : GoapGoal, IGoapEventListener
{
    public override float Cost => 4.6f;

    private const int MAX_TIME_TO_REACH_MELEE = 10000;

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
        AddPrecondition(GoapKey.dangercombat, false);
        AddPrecondition(GoapKey.shouldloot, true);
        AddEffect(GoapKey.shouldloot, false);
    }

    public override void OnEnter()
    {
        float e = wait.UntilCount(Loot.RESET_UPDATE_COUNT, LootReset);
        if (e < 0)
        {
            LogWarnWindowStillOpen(logger, playerReader.LootWindowCount.Value, e);
        }

        if (combatLog.DamageTakenCount() == 0)
        {
            WaitForLosingTarget();
        }

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

    private void WaitForLosingTarget()
    {
        float elapsedMs = wait.Until(playerReader.DoubleNetworkLatency, bits.NoTarget);

        LogLostTarget(logger, elapsedMs);
    }

    private void CheckInventoryFull()
    {
        if (!bagReader.BagsFull())
            return;

        logger.LogWarning("Inventory is full");
    }

    private bool TryLoot()
    {
        bool keyboardSuccessful = LootKeyboard();
        if (!keyboardSuccessful)
        {
            LogKeyboardLootFailed(logger, bits.Target());
        }
        else
        {
            return true;
        }

        return !input.KeyboardOnly && LootMouse();
    }

    private void HandleSuccessfulLoot()
    {
        int maxTimeLootWindowOpenMs =
            Math.Max(playerReader.DoubleNetworkLatency, Loot.LOOTFRAME_OPEN_TIME_MS);

        float windowOpenElapsedMs = wait.Until(maxTimeLootWindowOpenMs,
            LootWindowOpen,
            TryPressSafeApproachOnCooldownIfNeeded);

        int availableItems = playerReader.LootWindowCount.Value;
        state.RecentlyLooted.Add(playerReader.TargetGuid);

        int maxTimeLootWindowClosedMs =
            Math.Max(playerReader.LootWindowCount.Value, 1) *
            (playerReader.DoubleNetworkLatency + Loot.LOOT_PER_ITEM_TIME_MS);

        float windowClosedElapsedMs = wait.Until(maxTimeLootWindowClosedMs, LootWindowClosed);

        bool success = windowOpenElapsedMs >= 0 && windowClosedElapsedMs >= 0;
        if (success)
        {
            LogLootSuccess(logger, availableItems, windowOpenElapsedMs, windowClosedElapsedMs);
        }
        else
        {
            SendGoapEvent(ScreenCaptureEvent.Default);
            LogLootFailed(logger, windowOpenElapsedMs, windowClosedElapsedMs);
        }

        if (success)
        {
            GatherCorpseIfNeeded();
        }

        if (bits.LootFrameShown())
        {
            input.PressESC();
            wait.Update();
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

        if (bits.Target_Dead())
        {
            input.PressInteract();
            wait.Update();
        }

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

        ReadOnlySpan<CursorType> types = [CursorType.Loot, CursorType.Vendor];
        if (!npcNameTargeting.FindBy(types, token))
        {
            return false;
        }

        Log("Nearest Corpse clicked...");
        float elapsedMs = wait.Until(playerReader.NetworkLatency, bits.Target);
        LogFoundNpcNameCount(logger, npcNameTargeting.NpcCount, elapsedMs);

        npcNameTargeting.ChangeNpcType(NpcNames.None);

        CheckForCanGather();

        return (bits.Target() && playerReader.MinRangeZero()) || MoveToTargetAndReached();
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

    private bool LootWindowOpen()
    {
        return playerReader.LootWindowCount.Value > 0 ||
            (LootStatus)playerReader.LootEvent.Value is LootStatus.READY;
    }

    private bool LootWindowClosed() => !bits.LootFrameShown();

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
        }

        if (bits.Target())
        {
            int targetGuid = playerReader.TargetGuid;
            Log($"Keyboard last target {targetGuid}!");
            if (state.RecentlyLooted.Contains(targetGuid))
            {
                input.PressClearTarget();
                wait.Update();

                LogWarning($"Keyboard target already looted! {targetGuid}");
            }
            else
            {
                Log($"Keyboard last target found!");
            }
        }

        if (EligibleCorpseSoftTargetExists() && !state.RecentlyLooted.Contains(playerReader.SoftInteract_Guid))
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

        if (!bits.SoftInteract() || EligibleCorpseSoftTargetExists())
        {
            input.PressInteract();
            wait.Update();
        }

        return (bits.Target() && playerReader.MinRangeZero()) || MoveToTargetAndReached();
    }

    private bool EligibleCorpseSoftTargetExists() =>
        bits.SoftInteract() &&
        bits.SoftInteract_Hostile() &&
        bits.SoftInteract_Dead() &&
        !bits.SoftInteract_Tagged() &&
        playerReader.SoftInteract_Type == GuidType.Creature;

    private bool MoveToTargetAndReached()
    {
        if (!bits.Moving())
        {
            logger.LogInformation("Moving to corpse...");
            wait.While(input.Approach.OnCooldown);
            wait.Update();
        }

        float elapsedMs = wait.Until(MAX_TIME_TO_REACH_MELEE, //UntilWithoutRepeat
            NotMovingOrLootAvailable, TryPressSafeApproachOnCooldownIfNeeded);

        LogReachedCorpse(logger, bits.Target(), bits.Moving(), elapsedMs);

        return bits.Target() && playerReader.MinRangeZero();
    }

    private bool NotMovingOrLootAvailable() => !bits.Target() || bits.NotMoving() || playerReader.LootWindowCount.Value > 0;

    private void TryPressSafeApproachOnCooldownIfNeeded()
    {
        if (bits.Target() && (!bits.SoftInteract() || EligibleCorpseSoftTargetExists()))
        {
            if (!bits.Moving())
            {
                input.PressApproachOnCooldown();
                if (input.Approach.OnCooldown())
                {
                    wait.Update();
                    wait.Update();
                }
            }
        }
    }

    private bool LootReset()
    {
        return (LootStatus)playerReader.LootEvent.Value == LootStatus.CORPSE;
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
        Message = "Loot Successful items: {count} - open: {openElapsedMs}ms - close: {closedElapsedMs}ms")]
    static partial void LogLootSuccess(ILogger logger, int count, float openElapsedMs, float closedElapsedMs);

    [LoggerMessage(
        EventId = 0131,
        Level = LogLevel.Information,
        Message = "Loot Failed open: {openElapsedMs}ms - close: {closedElapsedMs}ms")]
    static partial void LogLootFailed(ILogger logger, float openElapsedMs, float closedElapsedMs);

    [LoggerMessage(
        EventId = 0132,
        Level = LogLevel.Information,
        Message = "Found NpcName Count: {npcCount} {elapsedMs}ms")]
    static partial void LogFoundNpcNameCount(ILogger logger, int npcCount, float elapsedMs);

    [LoggerMessage(
        EventId = 0133,
        Level = LogLevel.Information,
        Message = "Has target ? {hasTarget} | moving ? {moving} | Reached corpse ? {elapsedMs}ms")]
    static partial void LogReachedCorpse(ILogger logger, bool hasTarget, bool moving, float elapsedMs);

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

    [LoggerMessage(
        EventId = 0147,
        Level = LogLevel.Warning,
        Message = "OnEnter window still open! Available Loot: {count} {elapsedMs}ms")]
    static partial void LogWarnWindowStillOpen(ILogger logger, int count, float elapsedMs);

    #endregion
}
