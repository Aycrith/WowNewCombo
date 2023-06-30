using Game;

using Microsoft.Extensions.Logging;

using System;
using System.Threading;

using static System.Math;

namespace Core.Goals;

public sealed partial class CastingHandler
{
    private const bool Log = true;

    public const int GCD = 1500;
    public const int MIN_GCD = 1000;
    public const int SPELL_QUEUE = 400;

    private const int MAX_WAIT_MELEE_RANGE = 10_000;

    private readonly ILogger logger;
    private readonly ConfigurableInput input;

    private readonly Wait wait;
    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;
    private readonly BagReader bagReader;

    private readonly CombatLog combatLog;

    private readonly ActionBarBits<IUsableAction> usableAction;
    private readonly ActionBarBits<ICurrentAction> currentAction;

    private readonly ClassConfiguration classConfig;
    private readonly PlayerDirection direction;
    private readonly StopMoving stopMoving;

    private readonly ReactCastError react;

    private readonly CastingHandlerInterruptWatchdog interruptWatchdog;

    public DateTime SpellQueueOpen { get; private set; }
    public bool SpellInQueue() => DateTime.UtcNow < SpellQueueOpen;

    public static int _GCD() => GCD;

    public CastingHandler(ILogger logger, ConfigurableInput input,
        ClassConfiguration classConfig, AddonBits bits,
        ActionBarBits<IUsableAction> usableAction,
        ActionBarBits<ICurrentAction> currentAction,
        Wait wait,
        PlayerReader playerReader,
        BagReader bagReader,
        CombatLog combatLog,
        PlayerDirection direction,
        StopMoving stopMoving, ReactCastError react,
        CastingHandlerInterruptWatchdog interruptWatchdog)
    {
        this.logger = logger;
        this.input = input;

        this.wait = wait;
        this.bits = bits;
        this.playerReader = playerReader;
        this.bagReader = bagReader;

        this.combatLog = combatLog;

        this.usableAction = usableAction;
        this.currentAction = currentAction;

        this.classConfig = classConfig;
        this.direction = direction;
        this.stopMoving = stopMoving;

        this.react = react;


        this.interruptWatchdog = interruptWatchdog;
    }

    private int PressKeyAction(KeyAction item, CancellationToken token)
    {
        playerReader.LastUIError = UI_ERROR.NONE;

        if (item.AfterCastWaitSwing)
        {
            if (Log && item.Log)
                LogAfterCastWaitSwing(logger, item.Name);

            input.PressStopAttack();
        }

        DateTime start = DateTime.UtcNow;
        input.PressRandom(item, token);
        return (int)(DateTime.UtcNow - start).TotalMilliseconds;
    }

    private static bool CastSuccessful(int uiEvent)
    {
        return
            (UI_ERROR)uiEvent is
            UI_ERROR.CAST_START or
            UI_ERROR.CAST_SUCCESS or
            UI_ERROR.NONE or
            UI_ERROR.SPELL_FAILED_TARGETS_DEAD;
    }

    private bool CastInstant(KeyAction item, CancellationToken token)
    {
        if (!playerReader.IsCasting() && item.BeforeCastStop)
        {
            stopMoving.Stop();
            wait.Update();
        }

        playerReader.CastEvent.ForceUpdate(0);
        int beforeCastEventValue = playerReader.CastEvent.Value;
        int beforeSpellId = playerReader.CastSpellId.Value;
        bool beforeUsable = usableAction.Is(item);
        int beforePT = playerReader.PTCurrent();

        int pressMs = PressKeyAction(item, token);
        if (item.BaseAction)
        {
            item.SetClicked();

            if (Log && item.Log)
                LogInstantInput(logger, item.Name, pressMs, pressMs);

            return true;
        }

        float elapsedMs;

        if (item.AfterCastWaitSwing)
        {
            elapsedMs = wait.Until(playerReader.MainHandSpeedMs() + playerReader.NetworkLatency,
                interrupt: () => !currentAction.Is(item) ||
                    playerReader.MainHandSwing.ElapsedMs() < playerReader.SpellQueueTimeMs, // swing timer reset from any miss
                repeat: input.PressApproachOnCooldown);
        }
        else if (item.Item)
        {
            elapsedMs = wait.Until(SPELL_QUEUE + playerReader.NetworkLatency,
                interrupt: () =>
                    beforeUsable != usableAction.Is(item) ||
                    currentAction.Is(item));
        }
        else
        {
            elapsedMs = wait.Until(SPELL_QUEUE + playerReader.NetworkLatency,
                interrupt: () =>
                (beforeSpellId != playerReader.CastSpellId.Value &&
                beforeCastEventValue != playerReader.CastEvent.Value) ||
                beforePT != playerReader.PTCurrent());
        }

        if (Log && item.Log)
            LogInstantInput(logger, item.Name, pressMs, elapsedMs);

        if (elapsedMs < 0)
            return false;

        if (Log && item.Log)
            LogInstantUsableChange(logger, item.Name, beforeUsable, usableAction.Is(item), ((UI_ERROR)beforeCastEventValue).ToStringF(), ((UI_ERROR)playerReader.CastEvent.Value).ToStringF());

        if (!CastSuccessful(playerReader.CastEvent.Value))
        {
            react.Do(item);
            return false;
        }

        item.SetClicked();
        if (item.Item)
        {
            playerReader.LastCastGCD = 0;
            wait.Update();
        }
        else
            playerReader.ReadLastCastGCD();

        return true;
    }

    private bool CastCastbar(KeyAction item, CancellationToken token)
    {
        wait.While(bits.IsFalling);

        if (!playerReader.IsCasting())
        {
            stopMoving.Stop();
            wait.Update();
        }

        bool beforeUsable = usableAction.Is(item);
        int beforeCastEventValue = playerReader.CastEvent.Value;
        int beforeSpellId = playerReader.CastSpellId.Value;
        int beforeCastCount = playerReader.CastCount;

        int pressMs = PressKeyAction(item, token);

        if (item.BaseAction)
        {
            if (Log && item.Log)
                LogCastbarSkipValidation(logger, item.Name);

            item.SetClicked();
            return true;
        }

        float elapsedMs = wait.Until(SPELL_QUEUE + playerReader.NetworkLatency,
            interrupt: () =>
            beforeCastEventValue != playerReader.CastEvent.Value ||
            beforeSpellId != playerReader.CastSpellId.Value ||
            beforeCastCount != playerReader.CastCount ||
            token.IsCancellationRequested
            );

        if (Log && item.Log)
            LogCastbarInput(logger, item.Name, pressMs, elapsedMs);

        if (elapsedMs < 0)
            return false;

        if (Log && item.Log)
            LogCastbarUsableChange(logger, item.Name, playerReader.IsCasting(), playerReader.CastCount, beforeUsable, usableAction.Is(item), ((UI_ERROR)beforeCastEventValue).ToStringF(), ((UI_ERROR)playerReader.CastEvent.Value).ToStringF());

        if (!CastSuccessful(playerReader.CastEvent.Value))
        {
            react.Do(item);
            return false;
        }

        playerReader.LastCastGCD = 0;
        item.SetClicked();

        if (item.AfterCastWaitCastbar)
        {
            if (playerReader.IsCasting())
            {
                int remainMs = playerReader.RemainCastMs - playerReader.SpellQueueTimeMs;
                if (Log && item.Log)
                    LogVisibleAfterCastWaitCastbar(logger, item.Name, remainMs);

                wait.Until(remainMs, () => !playerReader.IsCasting() || token.IsCancellationRequested, RepeatPetAttack);
                if (token.IsCancellationRequested)
                {
                    if (Log && item.Log)
                        LogVisibleAfterCastWaitCastbarInterrupted(logger, item.Name);

                    return false;
                }
            }
            else if ((UI_ERROR)playerReader.CastEvent.Value == UI_ERROR.CAST_START)
            {
                beforeCastEventValue = playerReader.CastEvent.Value;

                int remainMs = playerReader.RemainCastMs - playerReader.SpellQueueTimeMs;
                if (Log && item.Log)
                    LogHiddenAfterCastWaitCastbar(logger, item.Name, remainMs);

                wait.Until(remainMs, () => beforeCastEventValue != playerReader.CastEvent.Value || token.IsCancellationRequested, RepeatPetAttack);
                if (token.IsCancellationRequested)
                {
                    if (Log && item.Log)
                        LogHiddenAfterCastWaitCastbarInterrupted(logger, item.Name);

                    return false;
                }
            }
        }

        return true;
    }

    public bool CastIfReady(KeyAction item, Func<bool> interrupt)
    {
        return item.CanRun() && Cast(item, interrupt);
    }

    public bool Cast(KeyAction item, Func<bool> interrupt)
    {
        using CancellationTokenSource cts = new();

        if (item.PressDuration > InputDuration.DefaultPress ||
            item.HasCastBar)
            interruptWatchdog.Set(interrupt, cts);

        float elapsedMs = 0;

        if (item.HasFormRequirement && playerReader.Form != item.FormEnum)
        {
            bool beforeUsable = usableAction.Is(item);
            Form beforeForm = playerReader.Form;

            if (!SwitchForm(item))
            {
                interruptWatchdog.Reset();
                return false;
            }

            if (!WaitForGCD(item, false, cts.Token))
            {
                interruptWatchdog.Reset();
                return false;
            }

            //TODO: upon form change and GCD - have to check Usable state
            if (!beforeUsable && !usableAction.Is(item))
            {
                if (Log && item.Log)
                    LogAfterFormSwitchNotUsable(logger, item.Name, beforeForm.ToStringF(), playerReader.Form.ToStringF());

                interruptWatchdog.Reset();
                return false;
            }
        }

        if (bits.SpellOn_Shoot())
        {
            input.PressStopAttack();
            input.PressStopAttack();

            int waitTime = Max(playerReader.GCD.Value, playerReader.RemainCastMs) + (2 * playerReader.NetworkLatency);
            elapsedMs = wait.Until(waitTime, cts.Token);
            logger.LogInformation($"Stop {nameof(bits.SpellOn_Shoot)} and wait {waitTime}ms | {elapsedMs}ms");
            if (elapsedMs >= 0)
            {
                interruptWatchdog.Reset();
                return false;
            }
        }

        if (!item.BaseAction && !WaitForGCD(item, true, cts.Token))
        {
            interruptWatchdog.Reset();
            return false;
        }

        if (item.BeforeCastDelay > 0)
        {
            if (!playerReader.IsCasting() && (item.BeforeCastStop || item.HasCastBar))
            {
                stopMoving.Stop();
                wait.Update();
            }

            if (Log && item.Log)
                LogBeforeCastDelay(logger, item.Name, item.BeforeCastDelay);

            wait.Until(item.BeforeCastDelay, cts.Token);
        }

        int auraHash = playerReader.AuraCount.Hash;
        int bagHash = bagReader.Hash;

        if (!item.HasCastBar)
        {
            if (!CastInstant(item, cts.Token))
            {
                // try again after reacted to UI_ERROR
                if (cts.Token.IsCancellationRequested || !CastInstant(item, cts.Token))
                {
                    interruptWatchdog.Reset();
                    return false;
                }
            }
        }
        else
        {
            if (!CastCastbar(item, cts.Token))
            {
                // try again after reacted to UI_ERROR
                if (cts.Token.IsCancellationRequested || !CastCastbar(item, cts.Token))
                {
                    interruptWatchdog.Reset();
                    return false;
                }
            }
        }

        if (item.AfterCastWaitBuff)
        {
            int totalTime = Max(playerReader.GCD.Value, playerReader.RemainCastMs) + playerReader.SpellQueueTimeMs;

            elapsedMs = wait.Until(totalTime, () =>
                auraHash != playerReader.AuraCount.Hash ||
                (MissType)combatLog.TargetMissType.Value != MissType.NONE ||
                cts.Token.IsCancellationRequested
                );

            if (Log && item.Log)
                LogAfterCastWaitBuff(logger, item.Name, playerReader.AuraCount.ToString(), ((MissType)combatLog.TargetMissType.Value).ToStringF(), elapsedMs);
        }

        if (item.AfterCastAuraExpected)
        {
            wait.Update();

            int delay = Max(playerReader.RemainCastMs, item.Item ? 0 : playerReader.LastCastGCD) + playerReader.SpellQueueTimeMs;

            if (Log && item.Log)
                LogAfterCastAuraExpected(logger, item.Name, delay);

            item.SetClicked(delay);
        }

        if (item.AfterCastWaitBag)
        {
            int totalTime = Max(playerReader.GCD.Value, playerReader.RemainCastMs) + playerReader.SpellQueueTimeMs;

            elapsedMs = wait.Until(totalTime,
                () => bagHash != bagReader.Hash || cts.Token.IsCancellationRequested);
            if (Log && item.Log)
                LogAfterCastWaitBag(logger, item.Name, elapsedMs);
        }

        if (item.AfterCastWaitCombat)
        {
            elapsedMs = wait.Until(2 * GCD,
                () => bits.PlayerInCombat() || cts.Token.IsCancellationRequested);

            if (Log && item.Log)
                LogAfterCastWaitCombat(logger, item.Name, elapsedMs);
        }

        if (item.AfterCastWaitMeleeRange)
        {
            int lastKnownHealth = playerReader.HealthCurrent();

            if (Log && item.Log)
                LogAfterCastWaitMeleeRange(logger, item.Name);

            wait.Until(MAX_WAIT_MELEE_RANGE,
                () =>
                playerReader.IsInMeleeRange() ||
                playerReader.IsTargetCasting() ||
                playerReader.HealthCurrent() < lastKnownHealth ||
                !playerReader.WithInPullRange() ||
                cts.Token.IsCancellationRequested
                );
        }

        if (item.AfterCastStepBack != 0)
        {
            if (Log && item.Log)
                LogAfterCastStepBack(logger, item.Name, item.AfterCastStepBack);

            input.StartBackward(true);

            if (Random.Shared.Next(3) == 0)
                input.PressJump();

            if (item.AfterCastStepBack == -1)
            {
                int waitAmount = playerReader.GCD.Value;
                if (waitAmount == 0)
                {
                    waitAmount = MIN_GCD - playerReader.SpellQueueTimeMs;
                }
                elapsedMs = wait.Until(waitAmount, cts.Token);
            }
            else
            {
                elapsedMs = wait.Until(item.AfterCastStepBack, cts.Token);
            }

            if (Log && item.Log)
                LogAfterCastStepBackInterrupted(logger, item.Name, elapsedMs);

            input.StopBackward(false);
        }

        if (item.AfterCastWaitGCD)
        {
            if (Log && item.Log)
                LogAfterCastWaitGCD(logger, item.Name, playerReader.GCD.Value);

            wait.Until(playerReader.GCD.Value, cts.Token);
        }

        if (item.AfterCastDelay > 0)
        {
            if (Log && item.Log)
                LogAfterCastDelay(logger, item.Name, item.AfterCastDelay);

            elapsedMs = wait.Until(item.AfterCastDelay, cts.Token);
            if (Log && item.Log && elapsedMs >= 0)
            {
                LogAfterCastDelayInterrupted(logger, item.Name, elapsedMs);
            }
        }

        if (!item.BaseAction)
        {
            int durationMs = UpdateGCD();
            if (Log && item.Log)
                LogWaitForGCD(logger, item.Name, playerReader.LastCastGCD,
                    playerReader.GCD.Value, playerReader.RemainCastMs, SpellQueueOpen, durationMs);
        }

        item.ConsumeCharge();

        interruptWatchdog.Reset();
        return true;
    }

    private bool WaitForGCD(KeyAction item, bool spellQueue, CancellationToken token) //Func<bool> interrupt
    {
        int duration = Max(playerReader.GCD.Value, playerReader.RemainCastMs);

        if (spellQueue)
            duration -= playerReader.SpellQueueTimeMs;

        if (duration <= 0)
            return true;

        float elapsedMs = wait.Until(duration, token);

        if (Log && item.Log)
            LogGCD(logger, item.Name, usableAction.Is(item), duration, elapsedMs);

        return elapsedMs < 0;
    }

    public int UpdateGCD()
    {
        int durationMs =
            Max(Max(playerReader.LastCastGCD, playerReader.RemainCastMs), playerReader.GCD.Value)
            - playerReader.SpellQueueTimeMs;

        SpellQueueOpen = DateTime.UtcNow.AddMilliseconds(durationMs);
        //logger.LogInformation($"Spell Queue window opens after {durationMs}");
        return durationMs;
    }

    public bool SwitchForm(KeyAction item)
    {
        KeyAction? formAction = null;
        for (int i = 0; i < classConfig.Form.Length; i++)
        {
            formAction = classConfig.Form[i];
            if (formAction.FormEnum == item.FormEnum)
            {
                break;
            }
        }

        if (formAction == null)
        {
            logger.LogError($"Unable to find {nameof(KeyAction.Key)} in {nameof(ClassConfiguration.Form)} to transform into {item.FormEnum}");
            return false;
        }

        return CastInstant(formAction, CancellationToken.None);
    }

    private void RepeatPetAttack()
    {
        if (bits.PlayerInCombat() &&
            bits.HasPet() &&
            !playerReader.PetHasTarget() &&
            input.PetAttack.GetRemainingCooldown() == 0)
        {
            input.PressPetAttack();
        }
    }

    #region Logging

    [LoggerMessage(
        EventId = 0070,
        Level = LogLevel.Information,
        Message = "[{name,-15}] ... AfterCastWaitSwing")]
    static partial void LogAfterCastWaitSwing(ILogger logger, string name);

    [LoggerMessage(
        EventId = 0071,
        Level = LogLevel.Information,
        Message = "[{name,-15}] instant skip validation")]
    static partial void LogInstantSkipValidation(ILogger logger, string name);

    [LoggerMessage(
        EventId = 0072,
        Level = LogLevel.Information,
        Message = "[{name,-15}] instant input {pressTime}ms {inputElapsedMs}ms")]
    static partial void LogInstantInput(ILogger logger, string name, int pressTime, float inputElapsedMs);

    [LoggerMessage(
        EventId = 0073,
        Level = LogLevel.Information,
        Message = "[{name,-15}] instant usable: {beforeUsable}->{afterUsable} | {beforeCastEvent}->{afterCastEvent}")]
    static partial void LogInstantUsableChange(ILogger logger, string name, bool beforeUsable, bool afterUsable, string beforeCastEvent, string afterCastEvent);

    [LoggerMessage(
        EventId = 0074,
        Level = LogLevel.Information,
        Message = "[{name,-15}] ... castbar skip validation")]
    static partial void LogCastbarSkipValidation(ILogger logger, string name);

    [LoggerMessage(
        EventId = 0075,
        Level = LogLevel.Information,
        Message = "[{name,-15}] castbar input {pressTime}ms {inputElapsedMs}ms")]
    static partial void LogCastbarInput(ILogger logger, string name, int pressTime, float inputElapsedMs);

    [LoggerMessage(
        EventId = 0076,
        Level = LogLevel.Information,
        Message = "[{name,-15}] ... casting: {casting} | count:{castCount} | usable: {beforeUsable}->{afterUsable} | {beforeCastEvent}->{afterCastEvent}")]
    static partial void LogCastbarUsableChange(ILogger logger, string name, bool casting, int castCount, bool beforeUsable, bool afterUsable, string beforeCastEvent, string afterCastEvent);

    [LoggerMessage(
        EventId = 0077,
        Level = LogLevel.Information,
        Message = "[{name,-15}] ... AfterCastWaitCastbar(V) {remain}ms or interrupt...")]
    static partial void LogVisibleAfterCastWaitCastbar(ILogger logger, string name, int remain);

    [LoggerMessage(
        EventId = 0078,
        Level = LogLevel.Warning,
        Message = "[{name,-15}] ... AfterCastWaitCastbar(v) interrupted!")]
    static partial void LogVisibleAfterCastWaitCastbarInterrupted(ILogger logger, string name);

    [LoggerMessage(
        EventId = 0079,
        Level = LogLevel.Information,
        Message = "[{name,-15}] ... AfterCastWaitCastbar(h) {remain}ms or interrupt...")]
    static partial void LogHiddenAfterCastWaitCastbar(ILogger logger, string name, int remain);

    [LoggerMessage(
        EventId = 0080,
        Level = LogLevel.Warning,
        Message = "[{name,-15}] ... AfterCastWaitCastbar(h) interrupted!")]
    static partial void LogHiddenAfterCastWaitCastbarInterrupted(ILogger logger, string name);


    [LoggerMessage(
        EventId = 0081,
        Level = LogLevel.Warning,
        Message = "[{name,-15}] ... after {before}->{after} form switch still not usable!")]
    static partial void LogAfterFormSwitchNotUsable(ILogger logger, string name, string before, string after);

    [LoggerMessage(
        EventId = 0082,
        Level = LogLevel.Information,
        Message = "[{name,-15}] ... BeforeCastDelay {delayBeforeCast}ms")]
    static partial void LogBeforeCastDelay(ILogger logger, string name, int delayBeforeCast);

    [LoggerMessage(
        EventId = 0083,
        Level = LogLevel.Information,
        Message = "[{name,-15}] ... AfterCastWaitBuff count: {auraCount} | miss: {missType} | {elapsedMs}ms")]
    static partial void LogAfterCastWaitBuff(ILogger logger, string name, string auraCount, string missType, float elapsedMs);

    [LoggerMessage(
        EventId = 0084,
        Level = LogLevel.Information,
        Message = "[{name,-15}] ... AfterCastWaitCombat {delayAfterCast}ms")]
    static partial void LogAfterCastWaitCombat(ILogger logger, string name, float delayAfterCast);

    [LoggerMessage(
        EventId = 0085,
        Level = LogLevel.Information,
        Message = "[{name,-15}] ... AfterCastDelay {delayAfterCast}ms")]
    static partial void LogAfterCastDelay(ILogger logger, string name, int delayAfterCast);

    [LoggerMessage(
        EventId = 0086,
        Level = LogLevel.Information,
        Message = "[{name,-15}] ... AfterCastDelay interrupted {delayElaspedMs}ms")]
    static partial void LogAfterCastDelayInterrupted(ILogger logger, string name, float delayElaspedMs);

    [LoggerMessage(
        EventId = 0088,
        Level = LogLevel.Information,
        Message = "[{name,-15}] ... AfterCastStepBack {stepBackAfterCast}ms")]
    static partial void LogAfterCastStepBack(ILogger logger, string name, int stepBackAfterCast);

    [LoggerMessage(
        EventId = 0089,
        Level = LogLevel.Information,
        Message = "[{name,-15}] .... AfterCastStepBack {stepbackElapsedMs}ms")]
    static partial void LogAfterCastStepBackInterrupted(ILogger logger, string name, float stepbackElapsedMs);

    [LoggerMessage(
        EventId = 0090,
        Level = LogLevel.Information,
        Message = "[{name,-15}] ... GCD usable: {usable} | remain: {remain}ms | {elapsedMs}ms")]
    static partial void LogGCD(ILogger logger, string name, bool usable, int remain, float elapsedMs);

    [LoggerMessage(
        EventId = 0091,
        Level = LogLevel.Information,
        Message = "[{name,-15}] ... form {before}->{after} | {elapsedMs}ms")]
    static partial void LogFormChanged(ILogger logger, string name, string before, string after, float elapsedMs);

    [LoggerMessage(
        EventId = 0092,
        Level = LogLevel.Information,
        Message = "[{name,-15}] ... AfterCastWaitBag {elapsedMs}ms")]
    static partial void LogAfterCastWaitBag(ILogger logger, string name, float elapsedMs);

    [LoggerMessage(
        EventId = 0093,
        Level = LogLevel.Information,
        Message = "[{name,-15}] PrevGCD: {prevGCD}ms | GCD: {gcd}ms | Cast: {remainCastMs}ms | {queueOpen:HH:mm:ss:fff} | Next spell {duration}ms")]
    static partial void LogWaitForGCD(ILogger logger, string name, int prevGCD, int gcd, int remainCastMs, DateTime queueOpen, float duration);

    [LoggerMessage(
        EventId = 0094,
        Level = LogLevel.Information,
        Message = "[{name,-15}] ... AfterCastAuraExpected {delay}ms")]
    static partial void LogAfterCastAuraExpected(ILogger logger, string name, int delay);

    [LoggerMessage(
        EventId = 0095,
        Level = LogLevel.Information,
        Message = "[{name,-15}] ... AfterCastWaitGCD {gcd}ms")]
    static partial void LogAfterCastWaitGCD(ILogger logger, string name, int gcd);

    [LoggerMessage(
        EventId = 0096,
        Level = LogLevel.Information,
        Message = "[{name,-15}] ... AfterCastWaitMeleeRange")]
    static partial void LogAfterCastWaitMeleeRange(ILogger logger, string name);

    #endregion
}