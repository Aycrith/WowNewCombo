using Microsoft.Extensions.Logging;
using System;
using System.Threading;

namespace Core.Goals
{
    public partial class CastingHandler
    {
        private const bool Log = true;

        private readonly ILogger logger;
        private readonly CancellationTokenSource cts;
        private readonly ConfigurableInput input;

        private readonly Wait wait;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;

        private readonly ClassConfiguration classConfig;
        private readonly PlayerDirection direction;
        private readonly StopMoving stopMoving;

        private readonly ReactCastError react;

        public const int GCD = 1500;
        public const int MIN_GCD = 1000;
        public const int SpellQueueTimeMs = 400;

        private const int MAX_WAIT_MELEE_RANGE = 10_000;

        public DateTime SpellQueueOpen { get; private set; }
        public bool SpellInQueue() => DateTime.UtcNow < SpellQueueOpen;

        public CastingHandler(ILogger logger, CancellationTokenSource cts,
            ConfigurableInput input, Wait wait, AddonReader addonReader,
            PlayerDirection direction, StopMoving stopMoving, ReactCastError react)
        {
            this.logger = logger;
            this.cts = cts;
            this.input = input;

            this.wait = wait;
            this.addonReader = addonReader;
            this.playerReader = addonReader.PlayerReader;

            this.classConfig = input.ClassConfig;
            this.direction = direction;
            this.stopMoving = stopMoving;

            this.react = react;
        }

        private int PressKeyAction(KeyAction item)
        {
            playerReader.LastUIError = UI_ERROR.NONE;

            if (item.AfterCastWaitSwing)
            {
                if (Log && item.Log)
                    LogAfterCastWaitSwing(logger, item.Name);

                input.StopAttack();
            }

            //item.SetClicked();
            return input.Proc.KeyPress(item.ConsoleKey, item.PressDuration);
        }

        private static bool CastSuccessful(int uiEvent)
        {
            return
                (UI_ERROR)uiEvent is
                UI_ERROR.CAST_START or
                UI_ERROR.CAST_SUCCESS or
                UI_ERROR.NONE;
        }

        private bool CastInstant(KeyAction item)
        {
            if (!playerReader.IsCasting() && item.BeforeCastStop)
            {
                stopMoving.Stop();
                wait.Update();
            }

            playerReader.CastEvent.ForceUpdate(0);
            int beforeCastEventValue = playerReader.CastEvent.Value;
            int beforeSpellId = playerReader.CastSpellId.Value;
            bool beforeUsable = addonReader.UsableAction.Is(item);
            int beforePT = playerReader.PTCurrent();

            int pressMs = PressKeyAction(item);
            if (item.BaseAction)
            {
                item.SetClicked();
                return true;
            }

            bool t;
            double e;

            if (item.AfterCastWaitSwing)
            {
                (t, e) = wait.Until(playerReader.MainHandSpeedMs() + playerReader.NetworkLatency.Value,
                    interrupt: () => !addonReader.CurrentAction.Is(item) ||
                        playerReader.MainHandSwing.ElapsedMs() < SpellQueueTimeMs, // swing timer reset from any miss
                    repeat: input.ApproachOnCooldown);
            }
            else if (item.Item)
            {
                (t, e) = wait.Until(SpellQueueTimeMs + playerReader.NetworkLatency.Value,
                    interrupt: () =>
                        beforeUsable != addonReader.UsableAction.Is(item) ||
                        addonReader.CurrentAction.Is(item));
            }
            else
            {
                (t, e) = wait.Until(SpellQueueTimeMs + playerReader.NetworkLatency.Value,
                    interrupt: () =>
                    (beforeSpellId != playerReader.CastSpellId.Value &&
                    beforeCastEventValue != playerReader.CastEvent.Value) ||
                    beforePT != playerReader.PTCurrent());
            }

            if (Log && item.Log)
                LogInstantInput(logger, item.Name, pressMs, !t, e);

            if (t)
                return false;

            if (Log && item.Log)
                LogInstantUsableChange(logger, item.Name, beforeUsable, addonReader.UsableAction.Is(item), ((UI_ERROR)beforeCastEventValue).ToStringF(), ((UI_ERROR)playerReader.CastEvent.Value).ToStringF());

            if (!CastSuccessful(playerReader.CastEvent.Value))
            {
                react.Do(item, $"{item.Name}-{nameof(CastingHandler)}: {nameof(CastInstant)}");
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

        private bool CastCastbar(KeyAction item, Func<bool> interrupt)
        {
            wait.While(playerReader.Bits.IsFalling);

            if (!playerReader.IsCasting())
            {
                stopMoving.Stop();
                wait.Update();
            }

            bool beforeUsable = addonReader.UsableAction.Is(item);
            int beforeCastEventValue = playerReader.CastEvent.Value;
            int beforeSpellId = playerReader.CastSpellId.Value;
            int beforeCastCount = playerReader.CastCount;

            int pressMs = PressKeyAction(item);

            if (item.BaseAction)
            {
                if (Log && item.Log)
                    LogCastbarSkipValidation(logger, item.Name);

                item.SetClicked();
                return true;
            }

            (bool t, double e) = wait.Until(SpellQueueTimeMs + playerReader.NetworkLatency.Value,
                interrupt: () =>
                beforeCastEventValue != playerReader.CastEvent.Value ||
                beforeSpellId != playerReader.CastSpellId.Value ||
                beforeCastCount != playerReader.CastCount ||
                interrupt()
                );

            if (Log && item.Log)
                LogCastbarInput(logger, item.Name, pressMs, !t, e);

            if (t)
                return false;

            if (Log && item.Log)
                LogCastbarUsableChange(logger, item.Name, playerReader.IsCasting(), playerReader.CastCount, beforeUsable, addonReader.UsableAction.Is(item), ((UI_ERROR)beforeCastEventValue).ToStringF(), ((UI_ERROR)playerReader.CastEvent.Value).ToStringF());

            if (!CastSuccessful(playerReader.CastEvent.Value))
            {
                react.Do(item, $"{item.Name}-{nameof(CastingHandler)}: {nameof(CastCastbar)}");
                return false;
            }

            playerReader.ReadLastCastGCD();
            item.SetClicked();

            if (item.AfterCastWaitCastbar)
            {
                if (playerReader.IsCasting())
                {
                    int remainMs = playerReader.RemainCastMs - SpellQueueTimeMs;
                    if (Log && item.Log)
                        LogVisibleAfterCastWaitCastbar(logger, item.Name, remainMs);

                    wait.Until(remainMs, () => !playerReader.IsCasting() || interrupt(), RepeatPetAttack);
                    if (interrupt())
                    {
                        if (Log && item.Log)
                            LogVisibleAfterCastWaitCastbarInterrupted(logger, item.Name);

                        return false;
                    }
                }
                else if ((UI_ERROR)playerReader.CastEvent.Value == UI_ERROR.CAST_START)
                {
                    beforeCastEventValue = playerReader.CastEvent.Value;

                    int remain = playerReader.RemainCastMs - SpellQueueTimeMs;
                    if (Log && item.Log)
                        LogHiddenAfterCastWaitCastbar(logger, item.Name, remain);

                    wait.Until(remain, () => beforeCastEventValue != playerReader.CastEvent.Value || interrupt(), RepeatPetAttack);
                    if (interrupt())
                    {
                        if (Log && item.Log)
                            LogHiddenAfterCastWaitCastbarInterrupted(logger, item.Name);

                        return false;
                    }
                }
            }

            return true;
        }

        public bool CastIfReady(KeyAction item)
        {
            return item.CanRun() && Cast(item, playerReader.Bits.HasTarget);
        }

        public bool CastIfReady(KeyAction item, Func<bool> interrupt)
        {
            return item.CanRun() && Cast(item, interrupt);
        }

        public bool Cast(KeyAction item)
        {
            return Cast(item, playerReader.Bits.HasTarget);
        }

        public bool Cast(KeyAction item, Func<bool> interrupt)
        {
            bool prevState = interrupt();
            bool Interrupt() => prevState != interrupt();

            bool t = false;
            double e = 0;

            if (item.HasFormRequirement && playerReader.Form != item.FormEnum)
            {
                bool beforeUsable = addonReader.UsableAction.Is(item);
                Form beforeForm = playerReader.Form;

                if (!SwitchForm(item))
                    return false;

                if (!WaitForGCD(item, Interrupt))
                    return false;

                //TODO: upon form change and GCD - have to check Usable state
                if (!beforeUsable && !addonReader.UsableAction.Is(item))
                {
                    if (Log && item.Log)
                        LogAfterFormSwitchNotUsable(logger, item.Name, beforeForm.ToStringF(), playerReader.Form.ToStringF());

                    return false;
                }
            }

            if (playerReader.Bits.SpellOn_Shoot())
            {
                input.StopAttack();
                input.StopAttack();

                int waitTime = Math.Max(playerReader.GCD.Value, playerReader.RemainCastMs) + (2 * playerReader.NetworkLatency.Value);
                (t, e) = wait.Until(waitTime, Interrupt);
                logger.LogInformation($"Stop {nameof(playerReader.Bits.SpellOn_Shoot)} and wait {waitTime}ms | {e}ms");
                if (!t)
                {
                    return false;
                }
            }

            if (!WaitForGCD(item, Interrupt))
            {
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

                wait.Until(item.BeforeCastDelay, Interrupt);
            }

            int auraHash = playerReader.AuraCount.Hash;
            int bagHash = addonReader.BagReader.Hash;

            if (!item.HasCastBar)
            {
                if (!CastInstant(item))
                {
                    // try again after reacted to UI_ERROR
                    if (!CastInstant(item))
                    {
                        return false;
                    }
                }
            }
            else
            {
                if (!CastCastbar(item, Interrupt))
                {
                    // try again after reacted to UI_ERROR
                    if (!CastCastbar(item, Interrupt))
                    {
                        return false;
                    }
                }
            }

            if (item.AfterCastWaitBuff)
            {
                int totalTime = Math.Max(playerReader.GCD.Value, playerReader.RemainCastMs) + SpellQueueTimeMs;

                (t, e) = wait.Until(totalTime, () =>
                    auraHash != playerReader.AuraCount.Hash ||
                    (MissType)addonReader.CombatLog.TargetMissType.Value != MissType.NONE ||
                    Interrupt()
                    );

                if (Log && item.Log)
                    LogAfterCastWaitBuff(logger, item.Name, !t, playerReader.AuraCount.ToString(), ((MissType)addonReader.CombatLog.TargetMissType.Value).ToStringF(), e);
            }

            if (item.AfterCastAuraExpected)
            {
                wait.Update();

                int delay = Math.Max(playerReader.RemainCastMs, item.Item ? 0 : playerReader.LastCastGCD) + SpellQueueTimeMs;

                if (Log && item.Log)
                    LogAfterCastAuraExpected(logger, item.Name, delay);

                item.SetClicked(delay);
            }

            if (item.AfterCastWaitBag)
            {
                int totalTime = Math.Max(playerReader.GCD.Value, playerReader.RemainCastMs) + SpellQueueTimeMs;

                (t, e) = wait.Until(totalTime, () => bagHash != addonReader.BagReader.Hash || Interrupt());
                if (Log && item.Log)
                    LogAfterCastWaitBag(logger, item.Name, !t, e);
            }

            if (item.AfterCastWaitCombat)
            {
                (t, e) = wait.Until(2 * GCD, () => playerReader.Bits.PlayerInCombat() || Interrupt());

                if (Log && item.Log)
                    LogAfterCastWaitCombat(logger, item.Name, !t, e);
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
                    Interrupt()
                    );
            }

            if (item.AfterCastStepBack != 0)
            {
                if (Log && item.Log)
                    LogAfterCastStepBack(logger, item.Name, item.AfterCastStepBack);

                input.Proc.SetKeyState(input.Proc.BackwardKey, true);

                if (Random.Shared.Next(3) == 0) // 33 %
                    input.Jump();

                if (item.AfterCastStepBack == -1)
                {
                    int waitAmount = playerReader.GCD.Value;
                    if (waitAmount == 0)
                    {
                        waitAmount = MIN_GCD - SpellQueueTimeMs;
                    }
                    (t, e) = wait.Until(waitAmount, Interrupt);
                }
                else
                {
                    (t, e) = wait.Until(item.AfterCastStepBack, Interrupt);
                }

                if (Log && item.Log)
                    LogAfterCastStepBackInterrupted(logger, item.Name, !t, e);

                input.Proc.SetKeyState(input.Proc.BackwardKey, false);
            }

            if (item.AfterCastWaitGCD)
            {
                if (Log && item.Log)
                    LogAfterCastWaitGCD(logger, item.Name, playerReader.GCD.Value);

                wait.Until(playerReader.GCD.Value, Interrupt);
            }

            if (item.AfterCastDelay > 0)
            {
                if (Log && item.Log)
                    LogAfterCastDelay(logger, item.Name, item.AfterCastDelay);

                (t, e) = wait.Until(item.AfterCastDelay, Interrupt);
                if (Log && item.Log && !t)
                {
                    LogAfterCastDelayInterrupted(logger, item.Name, e);
                }
            }

            int durationMs = UpdateGCD(false);

            if (Log && item.Log)
                LogWaitForGCD(logger, item.Name, playerReader.GCD.Value, playerReader.RemainCastMs, durationMs);

            item.ConsumeCharge();
            return true;
        }

        private bool WaitForGCD(KeyAction item, Func<bool> interrupt)
        {
            int duration = Math.Max(playerReader.GCD.Value, playerReader.RemainCastMs) - (SpellQueueTimeMs - playerReader.NetworkLatency.Value); //+ playerReader.NetworkLatency.Value
            if (duration < 0)
                return true;

            (bool t, double e) = wait.Until(duration, interrupt);

            if (Log && item.Log)
                LogGCD(logger, item.Name, !t, addonReader.UsableAction.Is(item), duration, e);

            return !t;
        }

        public int UpdateGCD(bool forced)
        {
            int durationMs;

            if (SpellInQueue() && !forced)
            {
                durationMs = Math.Max(playerReader.LastCastGCD, playerReader.RemainCastMs)
                    - SpellQueueTimeMs
                    + playerReader.NetworkLatency.Value;
            }
            else
            {
                durationMs = Math.Max(playerReader.GCD.Value, playerReader.RemainCastMs)
                    - SpellQueueTimeMs
                    + playerReader.NetworkLatency.Value;
            }

            SpellQueueOpen = DateTime.UtcNow.AddMilliseconds(durationMs);
            //logger.LogInformation($"Spell Queue window upens after {durationMs}");
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

            return CastInstant(formAction);
        }

        private void RepeatPetAttack()
        {
            if (playerReader.Bits.PlayerInCombat() &&
                playerReader.Bits.HasPet() &&
                !playerReader.PetHasTarget &&
                input.ClassConfig.PetAttack.GetCooldownRemaining() == 0)
            {
                input.PetAttack();
            }
        }

        #region Logging

        [LoggerMessage(
            EventId = 70,
            Level = LogLevel.Information,
            Message = "[{name}] ... AfterCastWaitSwing")]
        static partial void LogAfterCastWaitSwing(ILogger logger, string name);

        [LoggerMessage(
            EventId = 71,
            Level = LogLevel.Information,
            Message = "[{name}] instant skip validation")]
        static partial void LogInstantSkipValidation(ILogger logger, string name);

        [LoggerMessage(
            EventId = 72,
            Level = LogLevel.Information,
            Message = "[{name}] instant input {pressTime}ms {register} {inputElapsedMs}ms")]
        static partial void LogInstantInput(ILogger logger, string name, int pressTime, bool register, double inputElapsedMs);

        [LoggerMessage(
            EventId = 73,
            Level = LogLevel.Information,
            Message = "[{name}] instant usable: {beforeUsable}->{afterUsable} -- {beforeCastEvent}->{afterCastEvent}")]
        static partial void LogInstantUsableChange(ILogger logger, string name, bool beforeUsable, bool afterUsable, string beforeCastEvent, string afterCastEvent);

        [LoggerMessage(
            EventId = 74,
            Level = LogLevel.Information,
            Message = "[{name}] ... castbar skip validation")]
        static partial void LogCastbarSkipValidation(ILogger logger, string name);

        [LoggerMessage(
            EventId = 75,
            Level = LogLevel.Information,
            Message = "[{name}] castbar input {pressTime}ms {register} {inputElapsedMs}ms")]
        static partial void LogCastbarInput(ILogger logger, string name, int pressTime, bool register, double inputElapsedMs);

        [LoggerMessage(
            EventId = 76,
            Level = LogLevel.Information,
            Message = "[{name}] ... casting: {casting} -- count:{castCount} -- usable: {beforeUsable}->{afterUsable} -- {beforeCastEvent}->{afterCastEvent}")]
        static partial void LogCastbarUsableChange(ILogger logger, string name, bool casting, int castCount, bool beforeUsable, bool afterUsable, string beforeCastEvent, string afterCastEvent);

        [LoggerMessage(
            EventId = 77,
            Level = LogLevel.Information,
            Message = "[{name}] ... AfterCastWaitCastbar(V) {remain}ms or interrupt...")]
        static partial void LogVisibleAfterCastWaitCastbar(ILogger logger, string name, int remain);

        [LoggerMessage(
            EventId = 78,
            Level = LogLevel.Warning,
            Message = "[{name}] ... AfterCastWaitCastbar(v) interrupted!")]
        static partial void LogVisibleAfterCastWaitCastbarInterrupted(ILogger logger, string name);

        [LoggerMessage(
            EventId = 79,
            Level = LogLevel.Information,
            Message = "[{name}] ... AfterCastWaitCastbar(h) {remain}ms or interrupt...")]
        static partial void LogHiddenAfterCastWaitCastbar(ILogger logger, string name, int remain);

        [LoggerMessage(
            EventId = 80,
            Level = LogLevel.Warning,
            Message = "[{name}] ... AfterCastWaitCastbar(h) interrupted!")]
        static partial void LogHiddenAfterCastWaitCastbarInterrupted(ILogger logger, string name);


        [LoggerMessage(
            EventId = 81,
            Level = LogLevel.Warning,
            Message = "[{name}] ... after {before}->{after} form switch still not usable!")]
        static partial void LogAfterFormSwitchNotUsable(ILogger logger, string name, string before, string after);

        [LoggerMessage(
            EventId = 82,
            Level = LogLevel.Information,
            Message = "[{name}] ... BeforeCastDelay {delayBeforeCast}ms")]
        static partial void LogBeforeCastDelay(ILogger logger, string name, int delayBeforeCast);

        [LoggerMessage(
            EventId = 83,
            Level = LogLevel.Information,
            Message = "[{name}] ... AfterCastWaitBuff | Buff: {changeTimeOut} | {auraCount} | miss: {missType} | {elapsedMs}ms")]
        static partial void LogAfterCastWaitBuff(ILogger logger, string name, bool changeTimeOut, string auraCount, string missType, double elapsedMs);

        [LoggerMessage(
            EventId = 84,
            Level = LogLevel.Information,
            Message = "[{name}] ... AfterCastWaitCombat ? {success} {delayAfterCast}ms")]
        static partial void LogAfterCastWaitCombat(ILogger logger, string name, bool success, double delayAfterCast);

        [LoggerMessage(
            EventId = 85,
            Level = LogLevel.Information,
            Message = "[{name}] ... AfterCastDelay {delayAfterCast}ms")]
        static partial void LogAfterCastDelay(ILogger logger, string name, int delayAfterCast);

        [LoggerMessage(
            EventId = 86,
            Level = LogLevel.Information,
            Message = "[{name}] ... AfterCastDelay interrupted {delayElaspedMs}ms")]
        static partial void LogAfterCastDelayInterrupted(ILogger logger, string name, double delayElaspedMs);

        [LoggerMessage(
            EventId = 88,
            Level = LogLevel.Information,
            Message = "[{name}] ... AfterCastStepBack {stepBackAfterCast}ms")]
        static partial void LogAfterCastStepBack(ILogger logger, string name, int stepBackAfterCast);

        [LoggerMessage(
            EventId = 89,
            Level = LogLevel.Information,
            Message = "[{name}] .... AfterCastStepBack interrupt: {interrupt} | {stepbackElapsedMs}ms")]
        static partial void LogAfterCastStepBackInterrupted(ILogger logger, string name, bool interrupt, double stepbackElapsedMs);

        [LoggerMessage(
            EventId = 90,
            Level = LogLevel.Information,
            Message = "[{name}] ... gcd interrupt: {interrupt} | usable: {usable} | remain: {remain}ms | {elapsedMs}ms")]
        static partial void LogGCD(ILogger logger, string name, bool interrupt, bool usable, int remain, double elapsedMs);

        [LoggerMessage(
            EventId = 91,
            Level = LogLevel.Information,
            Message = "[{name}] ... form changed: {changedTimeOut} | {before}->{after} | {elapsedMs}ms")]
        static partial void LogFormChanged(ILogger logger, string name, bool changedTimeOut, string before, string after, double elapsedMs);

        [LoggerMessage(
            EventId = 92,
            Level = LogLevel.Information,
            Message = "[{name}] ... AfterCastWaitBag ? {changeTimeOut} | {elapsedMs}ms")]
        static partial void LogAfterCastWaitBag(ILogger logger, string name, bool changeTimeOut, double elapsedMs);

        [LoggerMessage(
            EventId = 93,
            Level = LogLevel.Information,
            Message = "[{name}] GCD: {gcd}ms | Cast: {remainCastMs}ms | Next spell {duration}ms")]
        static partial void LogWaitForGCD(ILogger logger, string name, int gcd, int remainCastMs, double duration);

        [LoggerMessage(
            EventId = 94,
            Level = LogLevel.Information,
            Message = "[{name}] ... AfterCastAuraExpected {delay}ms")]
        static partial void LogAfterCastAuraExpected(ILogger logger, string name, int delay);

        [LoggerMessage(
            EventId = 95,
            Level = LogLevel.Information,
            Message = "[{name}] ... AfterCastWaitGCD {gcd}ms")]
        static partial void LogAfterCastWaitGCD(ILogger logger, string name, int gcd);

        [LoggerMessage(
            EventId = 96,
            Level = LogLevel.Information,
            Message = "[{name}] ... AfterCastWaitMeleeRange")]
        static partial void LogAfterCastWaitMeleeRange(ILogger logger, string name);

        #endregion
    }
}