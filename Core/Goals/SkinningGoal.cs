using Core.GOAP;
using SharedLib.NpcFinder;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Core.Goals;

public sealed partial class SkinningGoal : GoapGoal, IGoapEventListener, IDisposable
{
    public override float Cost => 4.4f;

    private const int MAX_ATTEMPTS = 5;
    private const int MAX_TIME_TO_REACH_MELEE = 10000;
    private const int MAX_TIME_TO_DETECT_LOOT = 2 * CastingHandler.GCD;
    private const int MAX_TIME_TO_DETECT_CAST = 2 * CastingHandler.GCD;
    private const int MAX_TIME_TO_WAIT_NPC_NAME = 1000;

    private readonly ILogger<SkinningGoal> logger;
    private readonly ConfigurableInput input;
    private readonly ClassConfiguration classConfig;
    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;
    private readonly Wait wait;
    private readonly StopMoving stopMoving;
    private readonly BagReader bagReader;
    private readonly EquipmentReader equipmentReader;
    private readonly NpcNameTargeting npcNameTargeting;
    private readonly CombatUtil combatUtil;
    private readonly GoapAgentState state;

    private bool canRun;
    private int bagHashNewOrStackGain;

    private readonly List<SkinCorpseEvent> corpses = new();

    public SkinningGoal(ILogger<SkinningGoal> logger, ConfigurableInput input,
        PlayerReader playerReader,
        BagReader bagReader, EquipmentReader equipmentReader,
        AddonBits bits, Wait wait, StopMoving stopMoving,
        NpcNameTargeting npcNameTargeting, CombatUtil combatUtil,
        GoapAgentState state, ClassConfiguration classConfig)
        : base(nameof(SkinningGoal))
    {
        this.logger = logger;
        this.input = input;
        this.classConfig = classConfig;
        this.playerReader = playerReader;
        this.bits = bits;
        this.wait = wait;
        this.stopMoving = stopMoving;
        this.bagReader = bagReader;
        this.equipmentReader = equipmentReader;

        this.npcNameTargeting = npcNameTargeting;
        this.combatUtil = combatUtil;
        this.state = state;

        canRun = HaveItemRequirement();
        bagReader.DataChanged -= BagReader_DataChanged;
        bagReader.DataChanged += BagReader_DataChanged;
        equipmentReader.OnEquipmentChanged -= EquipmentReader_OnEquipmentChanged;
        equipmentReader.OnEquipmentChanged += EquipmentReader_OnEquipmentChanged;

        //AddPrecondition(GoapKey.dangercombat, false);

        AddPrecondition(GoapKey.shouldgather, true);
        AddEffect(GoapKey.shouldgather, false);
    }

    public void Dispose()
    {
        bagReader.DataChanged -= BagReader_DataChanged;
        equipmentReader.OnEquipmentChanged -= EquipmentReader_OnEquipmentChanged;
    }

    public override bool CanRun() => canRun;

    public void OnGoapEvent(GoapEventArgs e)
    {
        if (e is SkinCorpseEvent corpseEvent)
        {
            corpses.Add(corpseEvent);
        }
    }

    public override void OnEnter()
    {
        combatUtil.Update();

        float e = wait.Until(CastingHandler.GCD, LootReset);
        if (e < 0)
        {
            LogWarnWindowStillOpen(logger, e);
            ExitInterruptOrFailed(false);
            return;
        }

        bagHashNewOrStackGain = bagReader.HashNewOrStackGain;

        wait.Fixed(playerReader.NetworkLatency);

        if (bagReader.BagsFull())
        {
            LogWarning("Inventory is full!");
        }

        ReadOnlySpan<CursorType> types = stackalloc[] {
            CursorType.Skin,
            CursorType.Mine,
            CursorType.Herb
        };

        int attempts = 0;
        while (attempts < MAX_ATTEMPTS)
        {
            bool foundTarget = bits.Target() && bits.Target_Dead();

            if (!foundTarget && state.LastCombatKillCount == 1)
            {
                input.PressFastLastTarget();
                wait.Update();

                if (bits.Target())
                {
                    if (bits.Target_Dead())
                    {
                        foundTarget = true;
                        Log("Last Target found!");
                    }
                    else
                    {
                        Log("Last Target is alive!");
                        input.PressClearTarget();
                        wait.Update();
                    }
                }
            }

            bool interact = false;
            if (!foundTarget && !input.KeyboardOnly)
            {
                stopMoving.Stop();
                combatUtil.Update();

                npcNameTargeting.ChangeNpcType(NpcNames.Corpse);
                e = wait.Until(MAX_TIME_TO_WAIT_NPC_NAME, npcNameTargeting.FoundAny);
                LogFoundNpcNameCount(logger, npcNameTargeting.NpcCount, e);

                foundTarget = npcNameTargeting.FindBy(types); // todo salvage icon
                interact = true;
            }

            if (!foundTarget)
            {
                SendGoapEvent(ScreenCaptureEvent.Default);
                LogWarnUnableToTarget(logger, playerReader.TargetId);
                ExitInterruptOrFailed(false);
                return;
            }

            if (!playerReader.MinRangeZero())
            {
                e = wait.AfterEquals(MAX_TIME_TO_REACH_MELEE, 2, playerReader._MapPosNoZ,
                    input.PressApproachOnCooldown);

                LogReachedCorpse(logger, e);
                interact = !playerReader.MinRangeZero();
            }

            playerReader.LastUIError = 0;
            playerReader.CastEvent.ForceUpdate(0);

            e = wait.Until(MAX_TIME_TO_DETECT_CAST, CastStartedOrFailed, interact ? Empty : WhileNotCastingInteract);

            LogCastStartedOrInterrupted(logger, e >= 0, playerReader.IsCasting(), e);
            if (playerReader.LastUIError == UI_ERROR.ERR_REQUIRES_S)
            {
                LogWarning("Missing Spell/Item/Skill Requirement!");
                ExitInterruptOrFailed(false);
                return;
            }
            else if ((e < 0 || playerReader.LastUIError == UI_ERROR.ERR_LOOT_LOCKED) && !playerReader.IsCasting())
            {
                int delay = playerReader.LastUIError == UI_ERROR.ERR_LOOT_LOCKED
                    ? Loot.LOOTFRAME_AUTOLOOT_DELAY
                    : playerReader.NetworkLatency;

                wait.Fixed(delay);
                LogCastingState(logger, delay, playerReader.CastState.ToStringF(), playerReader.LastUIError.ToStringF(), playerReader.IsCasting());
                attempts++;

                ClearTargetIfExists();
                continue;
            }

            bool herbalism = Array.BinarySearch(GatherSpells.Herbalism, playerReader.SpellBeingCast) > -1;

            int remainMs = playerReader.RemainCastMs;
            playerReader.LastUIError = 0;

            int waitTime = remainMs + playerReader.SpellQueueTimeMs + playerReader.NetworkLatency;
            LogAwaitCastbarFinish(logger, herbalism ? "Herb Gathering" : "Skinning", waitTime);

            e = wait.Until(waitTime, herbalism ? HerbalismCastEnded : SkinningCastEnded);

            if (herbalism
                ? e < 0 || playerReader.LastUIError != UI_ERROR.SPELL_FAILED_TRY_AGAIN
                : playerReader.CastState == UI_ERROR.CAST_SUCCESS)
            {
                Log("Gathering Successful!");
                ExitSuccess();
                return;
            }
            else
            {
                if (combatUtil.EnteredCombat() ||
                playerReader.LastUIError == UI_ERROR.ERR_SPELL_FAILED_INTERRUPTED)
                {
                    Log("Interrupted due combat!");
                    ExitInterruptOrFailed(true);
                    return;
                }

                LogWarnGatherFailed(logger, playerReader.CastState.ToStringF(), attempts);
                wait.Fixed(Loot.LOOTFRAME_AUTOLOOT_DELAY);

                attempts++;

                ClearTargetIfExists();
            }
        }

        LogWarnOutOfAttempts(logger, attempts);
        ExitInterruptOrFailed(false);
    }

    public override void OnExit()
    {
        npcNameTargeting.ChangeNpcType(NpcNames.None);
    }

    private void ExitSuccess()
    {
        float e = wait.Until(MAX_TIME_TO_DETECT_LOOT, LootWindowClosedOrBagChanged);

        bool success = e >= 0 && !bagReader.BagsFull();
        if (success)
        {
            LogLootSuccess(logger, e);
            e = wait.Until(MAX_TIME_TO_WAIT_NPC_NAME, WaitForLosingTarget);
            if (e >= 0)
                ClearTargetIfExists();
        }
        else
        {
            SendGoapEvent(ScreenCaptureEvent.Default);
            LogLootFailed(logger, e);

            ClearTargetIfExists();
        }

        SendGoapEvent(new RemoveClosestPoi(SkinCorpseEvent.NAME));
        state.GatherableCorpseCount = Math.Max(0, state.GatherableCorpseCount - 1);
    }

    private void ExitInterruptOrFailed(bool interrupted)
    {
        if (!interrupted)
            state.GatherableCorpseCount = Math.Max(0, state.GatherableCorpseCount - 1);

        ClearTargetIfExists();
    }

    private void ClearTargetIfExists()
    {
        if (bits.Target() && bits.Target_Dead())
        {
            input.PressClearTarget();
            wait.Update();

            if (bits.Target())
            {
                SendGoapEvent(ScreenCaptureEvent.Default);
                LogWarning($"Unable to clear target! Check Bindpad settings!");
            }
        }
    }

    private void WhileNotCastingInteract()
    {
        if (!playerReader.IsCasting())
            input.PressApproachOnCooldown();
    }

    private static void Empty() { }

    private bool LootReset()
    {
        return (LootStatus)playerReader.LootEvent.Value == LootStatus.CORPSE;
    }

    private void EquipmentReader_OnEquipmentChanged(object? sender, (int, int) e)
    {
        canRun = HaveItemRequirement();
    }

    private void BagReader_DataChanged()
    {
        canRun = HaveItemRequirement();
    }

    private bool HaveItemRequirement()
    {
        if (classConfig.Herb) return true;

        if (classConfig.Skin)
        {
            return
            bagReader.HasItem(7005) ||
            bagReader.HasItem(12709) ||
            bagReader.HasItem(19901) ||
            bagReader.HasItem(40772) || // army knife
            bagReader.HasItem(40893) ||

            equipmentReader.HasItem(7005) ||
            equipmentReader.HasItem(12709) ||
            equipmentReader.HasItem(19901);
        }

        if (classConfig.Mine || classConfig.Salvage)
            return
            bagReader.HasItem(40772) || // army knife
                                        // mining / todo salvage
            bagReader.HasItem(40893) ||
            bagReader.HasItem(20723) ||
            bagReader.HasItem(1959) ||
            bagReader.HasItem(9465) ||
            bagReader.HasItem(1819) ||
            bagReader.HasItem(40892) ||
            bagReader.HasItem(778) ||
            bagReader.HasItem(1893) ||
            bagReader.HasItem(2901) ||
            bagReader.HasItem(756);

        return false;
    }

    private bool LootWindowClosedOrBagChanged()
    {
        return bagHashNewOrStackGain != bagReader.HashNewOrStackGain ||
            (LootStatus)playerReader.LootEvent.Value is
            LootStatus.CLOSED;
    }

    private bool SkinningCastEnded()
    {
        return
            playerReader.CastState is
            UI_ERROR.CAST_SUCCESS or
            UI_ERROR.SPELL_FAILED_TRY_AGAIN or
            UI_ERROR.ERR_SPELL_FAILED_INTERRUPTED;
    }

    private bool HerbalismCastEnded()
    {
        return
            playerReader.LastUIError is
            UI_ERROR.SPELL_FAILED_TRY_AGAIN or
            UI_ERROR.ERR_SPELL_FAILED_INTERRUPTED;
    }

    private bool CastStartedOrFailed()
    {
        return playerReader.IsCasting() ||
            playerReader.LastUIError is
            UI_ERROR.ERR_LOOT_LOCKED or
            UI_ERROR.ERR_REQUIRES_S;
    }

    private bool WaitForLosingTarget()
    {
        return
            bits.Target() &&
            bits.Target_Dead();
    }

    private void Log(string text)
    {
        logger.LogInformation(text);
    }

    private void LogWarning(string text)
    {
        logger.LogWarning(text);
    }

    #region Logging

    [LoggerMessage(
        EventId = 0140,
        Level = LogLevel.Warning,
        Message = "Loot window still open! {elapsedMs}ms")]
    static partial void LogWarnWindowStillOpen(ILogger logger, float elapsedMs);

    [LoggerMessage(
        EventId = 0141,
        Level = LogLevel.Information,
        Message = "Found NpcName Count: {npcCount} {elapsedMs}ms")]
    static partial void LogFoundNpcNameCount(ILogger logger, int npcCount, float elapsedMs);


    [LoggerMessage(
        EventId = 0142,
        Level = LogLevel.Warning,
        Message = "Unable to gather Target({targetId})!")]
    static partial void LogWarnUnableToTarget(ILogger logger, int targetId);

    [LoggerMessage(
        EventId = 0143,
        Level = LogLevel.Information,
        Message = "Reached corpse ? {elapsedMs}ms")]
    static partial void LogReachedCorpse(ILogger logger, float elapsedMs);

    [LoggerMessage(
        EventId = 0144,
        Level = LogLevel.Information,
        Message = "Started casting or interrupted ? {interrupt} - casting: {casting} {elapsedMs}ms")]
    static partial void LogCastStartedOrInterrupted(ILogger logger, bool interrupt, bool casting, float elapsedMs);

    [LoggerMessage(
        EventId = 0145,
        Level = LogLevel.Information,
        Message = "Wait {delay}ms and try again: {castState} | {uiError} | casting: {casting}")]
    static partial void LogCastingState(ILogger logger, int delay, string castState, string uiError, bool casting);

    [LoggerMessage(
        EventId = 0146,
        Level = LogLevel.Information,
        Message = "Waiting for {castName} castbar to end! {waitTime}ms")]
    static partial void LogAwaitCastbarFinish(ILogger logger, string castName, int waitTime);

    [LoggerMessage(
        EventId = 0147,
        Level = LogLevel.Warning,
        Message = "Gathering Failed! {castState} attempts: {attempts}")]
    static partial void LogWarnGatherFailed(ILogger logger, string castState, int attempts);

    [LoggerMessage(
        EventId = 0148,
        Level = LogLevel.Warning,
        Message = "Ran out of {attempts} maximum attempts...")]
    static partial void LogWarnOutOfAttempts(ILogger logger, int attempts);

    [LoggerMessage(
        EventId = 0149,
        Level = LogLevel.Information,
        Message = "Loot Successful {elapsedMs}ms")]
    static partial void LogLootSuccess(ILogger logger, float elapsedMs);

    [LoggerMessage(
        EventId = 0150,
        Level = LogLevel.Information,
        Message = "Loot Failed {elapsedMs}ms")]
    static partial void LogLootFailed(ILogger logger, float elapsedMs);



    #endregion
}
