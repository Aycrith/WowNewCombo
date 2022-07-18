using Microsoft.Extensions.Logging;
using System;
using System.Numerics;
using System.Threading;

namespace Core.Goals
{
    public partial class CastingHandler : IDisposable
    {
        private readonly ILogger logger;
        private readonly CancellationTokenSource cts;
        private readonly ConfigurableInput input;

        private readonly Wait wait;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;

        private readonly ClassConfiguration classConfig;
        private readonly PlayerDirection direction;
        private readonly StopMoving stopMoving;

        private readonly KeyAction defaultKeyAction = new();

        public const int GCD = 1500;
        public const int SpellQueueTimeMs = 400;
        public const int GatherCastTimeMs = 3000;

        private const int MaxWaitCastTimeMs = GCD;
        private const int MaxWaitBuffTimeMs = GCD;
        private const int MaxCastTimeMs = 15000;
        private const int MaxAirTimeMs = 10000;

        public CastingHandler(ILogger logger, CancellationTokenSource cts, ConfigurableInput input, Wait wait, AddonReader addonReader, PlayerDirection direction, StopMoving stopMoving)
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
        }

        public void Dispose()
        {
            defaultKeyAction.Dispose();
        }

        private void PressKeyAction(KeyAction item)
        {
            playerReader.LastUIError = UI_ERROR.NONE;

            if (item.AfterCastWaitNextSwing)
            {
                if (item.Log)
                    LogWaitNextSwing(logger, item.Name);

                input.StopAttack();
            }

            input.KeyPress(item.ConsoleKey, item.PressDuration);
            item.SetClicked();
        }

        private static bool CastSuccessfull(UI_ERROR uiEvent)
        {
            return
                uiEvent is
                UI_ERROR.CAST_START or
                UI_ERROR.CAST_SUCCESS or
                UI_ERROR.NONE;
        }

        private bool CastInstant(KeyAction item)
        {
            if (item.StopBeforeCast)
            {
                stopMoving.Stop();
                wait.Update();
            }

            playerReader.CastEvent.ForceUpdate(0);
            int beforeCastEventValue = playerReader.CastEvent.Value;
            int beforeSpellId = playerReader.CastSpellId.Value;
            bool beforeUsable = addonReader.UsableAction.Is(item);

            PressKeyAction(item);

            if (item.SkipValidation)
            {
                if (item.Log)
                    LogInstantSkipValidation(logger, item.Name);

                return true;
            }

            bool inputTimeOut;
            double inputElapsedMs;

            if (item.AfterCastWaitNextSwing)
            {
                (inputTimeOut, inputElapsedMs) = wait.Until(playerReader.MainHandSpeedMs(),
                    interrupt: () => !addonReader.CurrentAction.Is(item) ||
                        playerReader.MainHandSwing.ElapsedMs() < SpellQueueTimeMs, // swing timer reset from any miss
                    repeat: RepeatStayCloseToTarget);
            }
            else
            {
                (inputTimeOut, inputElapsedMs) = wait.Until(MaxWaitCastTimeMs,
                    interrupt: () =>
                    (beforeSpellId != playerReader.CastSpellId.Value && beforeCastEventValue != playerReader.CastEvent.Value) ||
                    beforeUsable != addonReader.UsableAction.Is(item)
                );
            }

            if (item.Log)
                LogInstantInput(logger, item.Name, !inputTimeOut, inputElapsedMs);

            if (inputTimeOut)
                return false;

            if (item.Log)
                LogInstantUsableChange(logger, item.Name, beforeUsable, addonReader.UsableAction.Is(item), ((UI_ERROR)beforeCastEventValue).ToStringF(), ((UI_ERROR)playerReader.CastEvent.Value).ToStringF());

            if (!CastSuccessfull((UI_ERROR)playerReader.CastEvent.Value))
            {
                ReactToLastCastingEvent(item, $"{item.Name}-{nameof(CastingHandler)}: CastInstant");
                return false;
            }
            return true;
        }

        private bool CastCastbar(KeyAction item, Func<bool> interrupt)
        {
            if (playerReader.Bits.IsFalling())
            {
                (bool fallTimeOut, double fallElapsedMs) = wait.UntilNot(MaxAirTimeMs, playerReader.Bits.IsFalling);
                if (!fallTimeOut)
                {
                    if (item.Log)
                        LogCastbarWaitForLand(logger, item.Name, fallElapsedMs);
                }
            }

            if (playerReader.IsCasting() && interrupt())
            {
                return false;
            }

            stopMoving.Stop();
            wait.Update();

            bool prevState = interrupt();

            bool beforeUsable = addonReader.UsableAction.Is(item);
            int beforeCastEventValue = playerReader.CastEvent.Value;
            int beforeSpellId = playerReader.CastSpellId.Value;
            int beforeCastCount = playerReader.CastCount;

            PressKeyAction(item);

            if (item.SkipValidation)
            {
                if (item.Log)
                    LogCastbarSkipValidation(logger, item.Name);

                return true;
            }

            (bool inputTimeOut, double inputElapsedMs) = wait.Until(MaxWaitCastTimeMs,
                interrupt: () =>
                beforeCastEventValue != playerReader.CastEvent.Value ||
                beforeSpellId != playerReader.CastSpellId.Value ||
                beforeCastCount != playerReader.CastCount
                );

            if (item.Log)
                LogCastbarInput(logger, item.Name, !inputTimeOut, inputElapsedMs);

            if (inputTimeOut)
                return false;

            if (item.Log)
                LogCastbarUsableChange(logger, item.Name, playerReader.IsCasting(), playerReader.CastCount, beforeUsable, addonReader.UsableAction.Is(item), ((UI_ERROR)beforeCastEventValue).ToStringF(), ((UI_ERROR)playerReader.CastEvent.Value).ToStringF());

            if (!CastSuccessfull((UI_ERROR)playerReader.CastEvent.Value))
            {
                ReactToLastCastingEvent(item, $"{item.Name}-{nameof(CastingHandler)}: CastCastbar");
                return false;
            }

            if (playerReader.IsCasting())
            {
                if (item.Log)
                    LogVisibleCastbarWaitForEnd(logger, item.Name);

                wait.Until(MaxCastTimeMs, () => !playerReader.IsCasting() || prevState != interrupt(), RepeatPetAttack);
                if (prevState != interrupt())
                {
                    if (item.Log)
                        LogVisibleCastbarInterrupted(logger, item.Name);

                    return false;
                }
            }
            else if ((UI_ERROR)playerReader.CastEvent.Value == UI_ERROR.CAST_START)
            {
                beforeCastEventValue = playerReader.CastEvent.Value;

                if (item.Log)
                    LogHiddenCastbarWaitForEnd(logger, item.Name);

                wait.Until(MaxCastTimeMs, () => beforeCastEventValue != playerReader.CastEvent.Value || prevState != interrupt(), RepeatPetAttack);
                if (prevState != interrupt())
                {
                    if (item.Log)
                        LogHiddenCastbarInterrupted(logger, item.Name);

                    return false;
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
            if (item.HasFormRequirement() && playerReader.Form != item.FormEnum)
            {
                bool beforeUsable = addonReader.UsableAction.Is(item);
                Form beforeForm = playerReader.Form;

                if (!SwitchForm(beforeForm, item))
                {
                    return false;
                }

                if (beforeForm != playerReader.Form)
                {
                    if (!WaitForGCD(item, interrupt))
                        return false;

                    //TODO: upon form change and GCD - have to check Usable state
                    if (!beforeUsable && !addonReader.UsableAction.Is(item))
                    {
                        if (item.Log)
                            LogAfterFormSwitchNotUsable(logger, item.Name, beforeForm.ToStringF(), playerReader.Form.ToStringF());

                        return false;
                    }
                }
            }

            if (playerReader.Bits.SpellOn_Shoot())
            {
                logger.LogInformation($"Stop {nameof(playerReader.Bits.SpellOn_Shoot)}");
                input.StopAttack();
                input.StopAttack();
                wait.Update();
            }

            if (item.DelayBeforeCast > 0)
            {
                if (item.StopBeforeCast || item.HasCastBar)
                {
                    stopMoving.Stop();
                    wait.Update();
                }

                if (item.Log)
                    LogDelayBeforeCast(logger, item.Name, item.DelayBeforeCast);

                cts.Token.WaitHandle.WaitOne(item.DelayBeforeCast);
            }

            int auraHash = playerReader.AuraCount.Hash;
            bool prevState = interrupt();

            if (item.WaitForGCD && !WaitForGCD(item, interrupt))
            {
                return false;
            }

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
                if (!CastCastbar(item, interrupt))
                {
                    // try again after reacted to UI_ERROR
                    if (!CastCastbar(item, interrupt))
                    {
                        return false;
                    }
                }
            }

            if (item.AfterCastWaitBuff)
            {
                (bool changeTimeOut, double elapsedMs) = wait.Until(MaxWaitBuffTimeMs, () => auraHash != playerReader.AuraCount.Hash);
                if (item.Log)
                    LogAfterCastWaitBuff(logger, item.Name, !changeTimeOut, playerReader.AuraCount.ToString(), elapsedMs);
            }

            if (item.DelayAfterCast != defaultKeyAction.DelayAfterCast)
            {
                if (item.DelayUntilCombat) // stop waiting if the mob is targetting me
                {
                    if (item.Log)
                        LogDelayUntilCombat(logger, item.Name, item.DelayAfterCast);

                    wait.Until(item.DelayAfterCast, playerReader.Bits.TargetOfTargetIsPlayerOrPet);
                }
                else if (item.DelayAfterCast > 0)
                {
                    if (item.Log)
                        LogDelayAfterCast(logger, item.Name, item.DelayAfterCast);

                    (bool delayTimeOut, double delayElapsedMs) = wait.Until(item.DelayAfterCast, () => prevState != interrupt());
                    if (item.Log)
                    {
                        if (!delayTimeOut)
                        {
                            LogDelayAfterCastInterrupted(logger, item.Name, delayElapsedMs);
                        }
                    }
                }
            }
            else if (item.RequirementsRuntime.Length > 0)
            {
                (bool canRun, double canRunElapsedMs) = wait.UntilNot(SpellQueueTimeMs, item.CanRun);
                if (item.Log)
                    LogWaitForInGameFeedback(logger, item.Name, !canRun, item.CanRun(), canRunElapsedMs);
            }

            if (item.StepBackAfterCast > 0)
            {
                if (item.Log)
                    LogStepBackAfterCast(logger, item.Name, item.StepBackAfterCast);

                input.SetKeyState(input.BackwardKey, true);

                (bool stepbackTimeOut, double stepbackElapsedMs) =
                    wait.Until(item.StepBackAfterCast, () => prevState != interrupt());

                if (item.Log)
                    LogStepBackAfterCastInterrupted(logger, item.Name, !stepbackTimeOut, stepbackElapsedMs);

                input.SetKeyState(input.BackwardKey, false);
            }

            item.ConsumeCharge();
            return true;
        }

        private bool WaitForGCD(KeyAction item, Func<bool> interrupt)
        {
            bool before = interrupt();
            (bool timeout, double elapsedMs) = wait.Until(GCD,
                () => addonReader.UsableAction.Is(item) || before != interrupt());

            if (item.Log)
                LogGCD(logger, item.Name, !timeout, elapsedMs);

            return timeout || before == interrupt();
        }

        public bool SwitchForm(Form beforeForm, KeyAction item)
        {
            int index = -1;
            for (int i = 0; i < classConfig.Form.Length; i++)
            {
                if (classConfig.Form[i].FormEnum == item.FormEnum)
                {
                    index = i;
                    break;
                }
            }
            if (index == -1)
            {
                logger.LogError($"Unable to find {nameof(KeyAction.Key)} in {nameof(ClassConfiguration.Form)} to transform into {item.FormEnum}");
                return false;
            }

            PressKeyAction(classConfig.Form[index]);

            (bool changedTimeOut, double elapsedMs) = wait.Until(SpellQueueTimeMs, () => playerReader.Form == item.FormEnum);
            if (item.Log)
                LogFormChanged(logger, item.Name, !changedTimeOut, beforeForm.ToStringF(), playerReader.Form.ToStringF(), elapsedMs);

            return playerReader.Form == item.FormEnum;
        }

        public void ReactToLastUIErrorMessage(string source)
        {
            //var lastError = playerReader.LastUIErrorMessage;
            switch (playerReader.LastUIError)
            {
                case UI_ERROR.NONE:
                    break;
                case UI_ERROR.CAST_START:
                    break;
                case UI_ERROR.CAST_SUCCESS:
                    break;
                case UI_ERROR.ERR_SPELL_FAILED_STUNNED:
                    int debuffCount = playerReader.AuraCount.PlayerDebuff;
                    if (debuffCount != 0)
                    {
                        logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_SPELL_FAILED_STUNNED.ToStringF()} -- Wait till losing debuff!");
                        wait.While(() => debuffCount == playerReader.AuraCount.PlayerDebuff);

                        wait.Update();
                        playerReader.LastUIError = UI_ERROR.NONE;
                    }
                    else
                    {
                        logger.LogInformation($"{source} -- Didn't know how to react {UI_ERROR.ERR_SPELL_FAILED_STUNNED.ToStringF()} when PlayerDebuffCount: {debuffCount}");
                    }
                    break;
                case UI_ERROR.ERR_SPELL_OUT_OF_RANGE:
                    if (playerReader.Class == PlayerClassEnum.Hunter && playerReader.IsInMeleeRange())
                    {
                        logger.LogInformation($"{source} -- As a Hunter didn't know how to react {UI_ERROR.ERR_SPELL_OUT_OF_RANGE.ToStringF()}");
                        return;
                    }

                    logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_SPELL_OUT_OF_RANGE.ToStringF()} -- Face enemy and start moving forward");
                    input.Interact();
                    input.SetKeyState(input.ForwardKey, true);

                    wait.Update();
                    playerReader.LastUIError = UI_ERROR.NONE;
                    break;
                case UI_ERROR.ERR_BADATTACKFACING:

                    if (playerReader.IsInMeleeRange())
                    {
                        logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_BADATTACKFACING.ToStringF()} -- Interact!");
                        input.Interact();
                    }
                    else
                    {
                        logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_BADATTACKFACING.ToStringF()} -- Turning 180!");

                        float desiredDirection = playerReader.Direction + MathF.PI;
                        desiredDirection = desiredDirection > MathF.PI * 2 ? desiredDirection - (MathF.PI * 2) : desiredDirection;
                        direction.SetDirection(desiredDirection, Vector3.Zero);
                    }

                    wait.Update();
                    playerReader.LastUIError = UI_ERROR.NONE;
                    break;
                case UI_ERROR.SPELL_FAILED_MOVING:
                    logger.LogInformation($"{source} -- React to {UI_ERROR.SPELL_FAILED_MOVING.ToStringF()} -- Stop moving!");

                    stopMoving.Stop();
                    wait.Update();
                    playerReader.LastUIError = UI_ERROR.NONE;
                    break;
                case UI_ERROR.ERR_SPELL_FAILED_ANOTHER_IN_PROGRESS:
                    logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_SPELL_FAILED_ANOTHER_IN_PROGRESS.ToStringF()} -- Wait till casting!");
                    wait.While(playerReader.IsCasting);

                    wait.Update();
                    playerReader.LastUIError = UI_ERROR.NONE;
                    break;
                case UI_ERROR.ERR_SPELL_COOLDOWN:
                    logger.LogInformation($"{source} -- Cant react to {UI_ERROR.ERR_SPELL_FAILED_ANOTHER_IN_PROGRESS.ToStringF()}");

                    wait.Update();
                    playerReader.LastUIError = UI_ERROR.NONE;
                    break;
                case UI_ERROR.ERR_BADATTACKPOS:
                    if (playerReader.Bits.SpellOn_AutoAttack())
                    {
                        logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_BADATTACKPOS.ToStringF()} -- Interact!");
                        input.Interact();
                        stopMoving.Stop();
                        wait.Update();

                        playerReader.LastUIError = UI_ERROR.NONE;
                    }
                    else
                    {
                        logger.LogInformation($"{source} -- Didn't know how to React to {playerReader.LastUIError.ToStringF()}");
                    }
                    break;
                default:
                    logger.LogInformation($"{source} -- Didn't know how to React to {playerReader.LastUIError.ToStringF()}");
                    break;
                    //case UI_ERROR.ERR_SPELL_FAILED_S:
                    //case UI_ERROR.ERR_BADATTACKPOS:
                    //case UI_ERROR.ERR_SPELL_OUT_OF_RANGE:
                    //case UI_ERROR.ERR_AUTOFOLLOW_TOO_FAR:
                    //    this.playerReader.LastUIErrorMessage = UI_ERROR.NONE;
                    //    break;
            }
        }

        private void ReactToLastCastingEvent(KeyAction item, string source)
        {
            switch ((UI_ERROR)playerReader.CastEvent.Value)
            {
                case UI_ERROR.NONE:
                    break;
                case UI_ERROR.ERR_SPELL_FAILED_INTERRUPTED:
                    item.SetClicked();
                    break;
                case UI_ERROR.CAST_START:
                    break;
                case UI_ERROR.CAST_SUCCESS:
                    break;
                case UI_ERROR.ERR_SPELL_COOLDOWN:
                    logger.LogInformation($"{source} React to {UI_ERROR.ERR_SPELL_COOLDOWN.ToStringF()} -- wait until its ready");
                    bool before = addonReader.UsableAction.Is(item);
                    wait.While(() => before != addonReader.UsableAction.Is(item));

                    break;
                case UI_ERROR.ERR_SPELL_FAILED_STUNNED:
                    int debuffCount = playerReader.AuraCount.PlayerDebuff;
                    if (debuffCount != 0)
                    {
                        logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_SPELL_FAILED_STUNNED.ToStringF()} -- Wait till losing debuff!");
                        wait.While(() => debuffCount == playerReader.AuraCount.PlayerDebuff);
                    }
                    else
                    {
                        logger.LogInformation($"{source} -- Didn't know how to react {UI_ERROR.ERR_SPELL_FAILED_STUNNED.ToStringF()} when PlayerDebuffCount: {debuffCount}");
                    }

                    break;
                case UI_ERROR.ERR_SPELL_OUT_OF_RANGE:
                    if (playerReader.Class == PlayerClassEnum.Hunter && playerReader.IsInMeleeRange())
                    {
                        logger.LogInformation($"{source} -- As a Hunter didn't know how to react {UI_ERROR.ERR_SPELL_OUT_OF_RANGE.ToStringF()}");
                        return;
                    }

                    float minRange = playerReader.MinRange();
                    if (playerReader.Bits.PlayerInCombat() && playerReader.Bits.HasTarget() && !playerReader.IsTargetCasting())
                    {
                        wait.Update();
                        wait.Update();
                        if (playerReader.TargetTarget == TargetTargetEnum.Me)
                        {
                            logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_SPELL_OUT_OF_RANGE.ToStringF()} -- Just wait for the target to get in range.");

                            (bool timeout, double elapsedMs) = wait.Until(MaxWaitCastTimeMs,
                                () => minRange != playerReader.MinRange() || playerReader.IsTargetCasting()
                            );
                        }
                    }
                    else
                    {
                        double beforeDirection = playerReader.Direction;
                        input.Interact();
                        input.StopAttack();
                        stopMoving.Stop();
                        wait.Update();

                        if (beforeDirection != playerReader.Direction)
                        {
                            input.Interact();

                            (bool timeout, double elapsedMs) = wait.Until(MaxWaitCastTimeMs,
                                () => minRange != playerReader.MinRange());

                            logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_SPELL_OUT_OF_RANGE.ToStringF()} -- Approached target {minRange}->{playerReader.MinRange()}");
                        }
                        else
                        {
                            logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_SPELL_OUT_OF_RANGE.ToStringF()} -- Start moving forward");
                            input.SetKeyState(input.ForwardKey, true);
                        }
                    }

                    break;
                case UI_ERROR.ERR_BADATTACKFACING:
                    if (playerReader.IsInMeleeRange())
                    {
                        logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_BADATTACKFACING.ToStringF()} -- Interact!");
                        input.Interact();
                    }
                    else
                    {
                        logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_BADATTACKFACING.ToStringF()} -- Turning 180!");

                        float desiredDirection = playerReader.Direction + MathF.PI;
                        desiredDirection = desiredDirection > MathF.PI * 2 ? desiredDirection - (MathF.PI * 2) : desiredDirection;
                        direction.SetDirection(desiredDirection, Vector3.Zero);

                        wait.Update();
                    }

                    break;
                case UI_ERROR.SPELL_FAILED_MOVING:
                    logger.LogInformation($"{source} -- React to {UI_ERROR.SPELL_FAILED_MOVING.ToStringF()} -- Stop moving!");
                    stopMoving.Stop();
                    wait.Update();

                    break;
                case UI_ERROR.ERR_SPELL_FAILED_ANOTHER_IN_PROGRESS:
                    logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_SPELL_FAILED_ANOTHER_IN_PROGRESS.ToStringF()} -- Wait till casting!");
                    wait.While(playerReader.IsCasting);

                    break;
                case UI_ERROR.ERR_BADATTACKPOS:
                    if (playerReader.Bits.SpellOn_AutoAttack())
                    {
                        logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_BADATTACKPOS.ToStringF()} -- Interact!");
                        input.Interact();
                        stopMoving.Stop();
                        wait.Update();
                    }
                    else
                    {
                        logger.LogInformation($"{source} -- Didn't know how to React to {((UI_ERROR)playerReader.CastEvent.Value).ToStringF()}");
                    }

                    break;
                default:
                    logger.LogInformation($"{source} -- Didn't know how to React to {((UI_ERROR)playerReader.CastEvent.Value).ToStringF()}");

                    break;
                    //case UI_ERROR.ERR_SPELL_FAILED_S:
                    //case UI_ERROR.ERR_BADATTACKPOS:
                    //case UI_ERROR.ERR_SPELL_OUT_OF_RANGE:
                    //case UI_ERROR.ERR_AUTOFOLLOW_TOO_FAR:
                    //    this.playerReader.LastUIErrorMessage = UI_ERROR.NONE;
                    //    break;
            }
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

        private void RepeatStayCloseToTarget()
        {
            if (classConfig.Approach.GetCooldownRemaining() == 0)
            {
                input.Approach();
            }
        }


        #region Logging

        [LoggerMessage(
            EventId = 70,
            Level = LogLevel.Information,
            Message = "[{name}] waiting for next swing...")]
        static partial void LogWaitNextSwing(ILogger logger, string name);

        [LoggerMessage(
            EventId = 71,
            Level = LogLevel.Information,
            Message = "[{name}] instant skip validation")]
        static partial void LogInstantSkipValidation(ILogger logger, string name);

        [LoggerMessage(
            EventId = 72,
            Level = LogLevel.Information,
            Message = "[{name}] instant input {register} {inputElapsedMs}ms")]
        static partial void LogInstantInput(ILogger logger, string name, bool register, double inputElapsedMs);

        [LoggerMessage(
            EventId = 73,
            Level = LogLevel.Information,
            Message = "[{name}] ... usable: {beforeUsable}->{afterUsable} -- {beforeCastEvent}->{afterCastEvent}")]
        static partial void LogInstantUsableChange(ILogger logger, string name, bool beforeUsable, bool afterUsable, string beforeCastEvent, string afterCastEvent);


        [LoggerMessage(
            EventId = 74,
            Level = LogLevel.Information,
            Message = "[{name}] ... castbar waited for landing {fallElapsedMs}ms")]
        static partial void LogCastbarWaitForLand(ILogger logger, string name, double fallElapsedMs);

        [LoggerMessage(
            EventId = 75,
            Level = LogLevel.Information,
            Message = "[{name}] ... castbar skip validation")]
        static partial void LogCastbarSkipValidation(ILogger logger, string name);

        [LoggerMessage(
            EventId = 76,
            Level = LogLevel.Information,
            Message = "[{name}] ... castbar input {register} {inputElapsedMs}ms")]
        static partial void LogCastbarInput(ILogger logger, string name, bool register, double inputElapsedMs);

        [LoggerMessage(
            EventId = 77,
            Level = LogLevel.Information,
            Message = "[{name}] ... casting: {casting} -- count:{castCount} -- usable: {beforeUsable}->{afterUsable} -- {beforeCastEvent}->{afterCastEvent}")]
        static partial void LogCastbarUsableChange(ILogger logger, string name, bool casting, int castCount, bool beforeUsable, bool afterUsable, string beforeCastEvent, string afterCastEvent);

        [LoggerMessage(
            EventId = 78,
            Level = LogLevel.Information,
            Message = "[{name}] waiting for visible cast bar finish or interrupt...")]
        static partial void LogVisibleCastbarWaitForEnd(ILogger logger, string name);

        [LoggerMessage(
            EventId = 79,
            Level = LogLevel.Warning,
            Message = "[{name}] ... visible castbar interrupted!")]
        static partial void LogVisibleCastbarInterrupted(ILogger logger, string name);

        [LoggerMessage(
            EventId = 80,
            Level = LogLevel.Information,
            Message = "[{name}] waiting for hidden cast bar finish or interrupt...")]
        static partial void LogHiddenCastbarWaitForEnd(ILogger logger, string name);

        [LoggerMessage(
            EventId = 81,
            Level = LogLevel.Warning,
            Message = "[{name}] ... hidden castbar interrupted!")]
        static partial void LogHiddenCastbarInterrupted(ILogger logger, string name);


        [LoggerMessage(
            EventId = 82,
            Level = LogLevel.Warning,
            Message = "[{name}] ... after {before}->{after} form switch still not usable!")]
        static partial void LogAfterFormSwitchNotUsable(ILogger logger, string name, string before, string after);

        [LoggerMessage(
            EventId = 83,
            Level = LogLevel.Information,
            Message = "[{name}] DelayBeforeCast {delayBeforeCast}ms")]
        static partial void LogDelayBeforeCast(ILogger logger, string name, int delayBeforeCast);

        [LoggerMessage(
            EventId = 84,
            Level = LogLevel.Information,
            Message = "[{name}] ... AfterCastWaitBuff | Buff: {changeTimeOut} | {auraCount} | {elapsedMs}ms")]
        static partial void LogAfterCastWaitBuff(ILogger logger, string name, bool changeTimeOut, string auraCount, double elapsedMs);

        [LoggerMessage(
            EventId = 85,
            Level = LogLevel.Information,
            Message = "[{name}] ... DelayUntilCombat ... DelayAfterCast {delayAfterCast}ms")]
        static partial void LogDelayUntilCombat(ILogger logger, string name, int delayAfterCast);


        [LoggerMessage(
            EventId = 86,
            Level = LogLevel.Information,
            Message = "[{name}] ... DelayAfterCast {delayAfterCast}ms")]
        static partial void LogDelayAfterCast(ILogger logger, string name, int delayAfterCast);

        [LoggerMessage(
            EventId = 87,
            Level = LogLevel.Information,
            Message = "[{name}] ... DelayAfterCast interrupted {delayElaspedMs}ms")]
        static partial void LogDelayAfterCastInterrupted(ILogger logger, string name, double delayElaspedMs);

        [LoggerMessage(
            EventId = 88,
            Level = LogLevel.Information,
            Message = "[{name}] ... instant interrupt: {interrupt} | CanRun: {canRun} | {canRunElapsedMs}ms")]
        static partial void LogWaitForInGameFeedback(ILogger logger, string name, bool interrupt, bool canRun, double canRunElapsedMs);

        [LoggerMessage(
            EventId = 89,
            Level = LogLevel.Information,
            Message = "[{name}] StepBackAfterCast {stepBackAfterCast}ms")]
        static partial void LogStepBackAfterCast(ILogger logger, string name, int stepBackAfterCast);

        [LoggerMessage(
            EventId = 90,
            Level = LogLevel.Information,
            Message = "[{name}] .... StepBackAfterCast interrupt: {interrupt} | {stepbackElapsedMs}ms")]
        static partial void LogStepBackAfterCastInterrupted(ILogger logger, string name, bool interrupt, double stepbackElapsedMs);

        [LoggerMessage(
            EventId = 91,
            Level = LogLevel.Information,
            Message = "[{name}] ... gcd interrupt: {interrupt} | {elapsedMs}ms")]
        static partial void LogGCD(ILogger logger, string name, bool interrupt, double elapsedMs);

        [LoggerMessage(
            EventId = 92,
            Level = LogLevel.Information,
            Message = "[{name}] ... form changed: {changedTimeOut} | {before}->{after} | {elapsedMs}ms")]
        static partial void LogFormChanged(ILogger logger, string name, bool changedTimeOut, string before, string after, double elapsedMs);

        #endregion
    }
}