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
        private readonly AddonBits bits;

        private readonly ClassConfiguration classConfig;
        private readonly PlayerDirection direction;
        private readonly StopMoving stopMoving;

        private readonly KeyAction defaultKeyAction = new();

        public const int GCD = 1500;
        public const int MIN_GCD = 1000;
        public const int SpellQueueTimeMs = 400;

        public DateTime SpellQueueOpen { get; private set; }
        public bool SpellInQueue() => DateTime.UtcNow < SpellQueueOpen;

        public CastingHandler(ILogger logger, CancellationTokenSource cts, ConfigurableInput input, Wait wait, AddonReader addonReader, PlayerDirection direction, StopMoving stopMoving)
        {
            this.logger = logger;
            this.cts = cts;
            this.input = input;

            this.wait = wait;
            this.addonReader = addonReader;
            this.playerReader = addonReader.PlayerReader;
            this.bits = playerReader.Bits;

            this.classConfig = input.ClassConfig;
            this.direction = direction;
            this.stopMoving = stopMoving;
        }

        public void Dispose()
        {
            defaultKeyAction.Dispose();
        }

        private int PressKeyAction(KeyAction item)
        {
            playerReader.LastUIError = UI_ERROR.NONE;

            if (item.AfterCastWaitNextSwing)
            {
                if (item.Log)
                    LogWaitNextSwing(logger, item.Name);

                input.StopAttack();
            }

            return input.Proc.KeyPress(item.ConsoleKey, item.PressDuration);
        }

        private static bool CastSuccessful(UI_ERROR uiEvent)
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

            int pressTime = PressKeyAction(item);

            if (item.SkipValidation)
            {
                //if (item.Log)
                //    LogInstantSkipValidation(logger, item.Name);
                item.SetClicked();
                return true;
            }

            bool inputTimeOut;
            double inputElapsedMs;

            if (item.AfterCastWaitNextSwing)
            {
                (inputTimeOut, inputElapsedMs) = wait.Until(playerReader.MainHandSpeedMs() + playerReader.NetworkLatency.Value,
                    interrupt: () => !addonReader.CurrentAction.Is(item) ||
                        playerReader.MainHandSwing.ElapsedMs() < SpellQueueTimeMs, // swing timer reset from any miss
                    repeat: RepeatStayCloseToTarget);
            }
            else
            {
                (inputTimeOut, inputElapsedMs) = wait.Until(SpellQueueTimeMs + playerReader.NetworkLatency.Value,
                    interrupt: () =>
                    (beforeSpellId != playerReader.CastSpellId.Value && beforeCastEventValue != playerReader.CastEvent.Value) ||
                    beforeUsable != addonReader.UsableAction.Is(item)
                );
            }

            if (item.Log)
                LogInstantInput(logger, item.Name, pressTime, !inputTimeOut, inputElapsedMs);

            if (inputTimeOut)
                return false;

            if (item.Log)
                LogInstantUsableChange(logger, item.Name, beforeUsable, addonReader.UsableAction.Is(item), ((UI_ERROR)beforeCastEventValue).ToStringF(), ((UI_ERROR)playerReader.CastEvent.Value).ToStringF(), playerReader.GCD.Value);

            item.SetClicked();

            if (!CastSuccessful((UI_ERROR)playerReader.CastEvent.Value))
            {
                ReactToLastCastingEvent(item, $"{item.Name}-{nameof(CastingHandler)}: CastInstant");
                return false;
            }

            //wait.Update();
            //(bool gcdTimeout, double elapsedMs) = wait.Until(2 * playerReader.NetworkLatency.Value, () => playerReader.GCD.Value != 0);
            //logger.LogInformation($"Instant - has GCD ? {!gcdTimeout} | {playerReader.GCD.Value}ms | {elapsedMs}ms");

            return true;
        }

        private bool CastCastbar(KeyAction item, Func<bool> interrupt)
        {
            wait.While(bits.IsFalling);

            if (!playerReader.IsCasting())
            {
                stopMoving.Stop();
                wait.Update();
            }

            bool prevState = interrupt();

            bool beforeUsable = addonReader.UsableAction.Is(item);
            int beforeCastEventValue = playerReader.CastEvent.Value;
            int beforeSpellId = playerReader.CastSpellId.Value;
            int beforeCastCount = playerReader.CastCount;

            int pressMs = PressKeyAction(item);
            int remainCastMs = playerReader.RemainCastMs;

            if (item.SkipValidation)
            {
                if (item.Log)
                    LogCastbarSkipValidation(logger, item.Name);

                item.SetClicked();
                return true;
            }

            (bool inputTimeOut, double inputElapsedMs) = wait.Until(remainCastMs + pressMs + SpellQueueTimeMs + (2 * playerReader.NetworkLatency.Value),
                interrupt: () =>
                beforeCastEventValue != playerReader.CastEvent.Value ||
                beforeSpellId != playerReader.CastSpellId.Value ||
                beforeCastCount != playerReader.CastCount ||
                prevState != interrupt()
                );

            if (item.Log)
                LogCastbarInput(logger, item.Name, pressMs, !inputTimeOut, inputElapsedMs);

            if (inputTimeOut)
                return false;

            if (item.Log)
                LogCastbarUsableChange(logger, item.Name, playerReader.IsCasting(), playerReader.CastCount, beforeUsable, addonReader.UsableAction.Is(item), ((UI_ERROR)beforeCastEventValue).ToStringF(), ((UI_ERROR)playerReader.CastEvent.Value).ToStringF());

            item.SetClicked();

            if (!CastSuccessful((UI_ERROR)playerReader.CastEvent.Value))
            {
                ReactToLastCastingEvent(item, $"{item.Name}-{nameof(CastingHandler)}: CastCastbar");
                return false;
            }

            if (item.AfterCastWaitCastbar)
            {
                if (playerReader.IsCasting())
                {
                    int remainMs = playerReader.RemainCastMs;
                    remainMs -= (SpellQueueTimeMs / 2) + playerReader.NetworkLatency.Value;

                    if (item.Log)
                        LogVisibleCastbarWaitForEnd(logger, item.Name, remainMs);

                    wait.Until(remainMs, () => !playerReader.IsCasting() || prevState != interrupt(), RepeatPetAttack);
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

                    int remain = playerReader.RemainCastMs - SpellQueueTimeMs - playerReader.NetworkLatency.Value;

                    if (item.Log)
                        LogHiddenCastbarWaitForEnd(logger, item.Name, remain);

                    wait.Until(remain, () => beforeCastEventValue != playerReader.CastEvent.Value || prevState != interrupt(), RepeatPetAttack);
                    if (prevState != interrupt())
                    {
                        if (item.Log)
                            LogHiddenCastbarInterrupted(logger, item.Name);

                        return false;
                    }
                }
            }

            return true;
        }

        public bool CastIfReady(KeyAction item)
        {
            return item.CanRun() && Cast(item, bits.HasTarget);
        }

        public bool CastIfReady(KeyAction item, Func<bool> interrupt)
        {
            return item.CanRun() && Cast(item, interrupt);
        }

        public bool Cast(KeyAction item)
        {
            return Cast(item, bits.HasTarget);
        }

        public bool Cast(KeyAction item, Func<bool> interrupt)
        {
            DateTime begin = DateTime.UtcNow;

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

                    wait.Update();

                    //TODO: upon form change and GCD - have to check Usable state
                    if (!beforeUsable && !addonReader.UsableAction.Is(item))
                    {
                        if (item.Log)
                            LogAfterFormSwitchNotUsable(logger, item.Name, beforeForm.ToStringF(), playerReader.Form.ToStringF());

                        return false;
                    }
                }
            }

            bool prevState = interrupt();

            if (bits.SpellOn_Shoot())
            {
                input.StopAttack();
                input.StopAttack();

                int waitTime = Math.Max(playerReader.GCD.Value, playerReader.RemainCastMs) + (2 * playerReader.NetworkLatency.Value);
                (bool gcdTimeout, double elapsedMs) = wait.Until(waitTime, () => prevState != interrupt());
                logger.LogInformation($"Stop {nameof(bits.SpellOn_Shoot)} and wait {waitTime}ms | {elapsedMs}ms");
                if (!gcdTimeout)
                {
                    return false;
                }
            }

            if (item.WaitForGCD && !WaitForGCD(item, interrupt))
            {
                return false;
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
                // GCD means projectile travel time
                int totalTime = Math.Max(playerReader.GCD.Value, playerReader.RemainCastMs) + 2 * SpellQueueTimeMs;

                (bool changeTimeOut, double elapsedMs) = wait.Until(totalTime, () => auraHash != playerReader.AuraCount.Hash);
                if (item.Log)
                    LogAfterCastWaitBuff(logger, item.Name, !changeTimeOut, playerReader.AuraCount.ToString(), elapsedMs);
            }

            if (item.AfterCastWaitItem)
            {
                int totalTime = Math.Max(playerReader.GCD.Value, playerReader.RemainCastMs) + SpellQueueTimeMs;

                (bool changeTimeOut, double elapsedMs) = wait.Until(totalTime, () => bagHash != addonReader.BagReader.Hash);
                if (item.Log)
                    LogAfterCastWaitItem(logger, item.Name, !changeTimeOut, elapsedMs);
            }

            if (item.DelayUntilCombat) // stop waiting if the mob is targetting me
            {
                stopMoving.Stop();
                (bool timeout, double elapsedMs) = wait.Until(2 * item.DelayAfterCast, bits.TargetOfTargetIsPlayerOrPet); // possible travel speed projectile

                if (item.Log)
                    LogDelayUntilCombat(logger, item.Name, !timeout, elapsedMs);
            }

            if (item.DelayAfterCast != defaultKeyAction.DelayAfterCast && item.DelayAfterCast > 0)
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

            if (item.StepBackAfterCast != 0)
            {
                if (item.Log)
                    LogStepBackAfterCast(logger, item.Name, item.StepBackAfterCast);

                input.Proc.SetKeyState(input.Proc.BackwardKey, true);

                bool stepbackTimeOut = false;
                double stepbackElapsedMs = 0;

                if (Random.Shared.Next(3) == 0) // 33 %
                    input.Jump();

                if (item.StepBackAfterCast == -1)
                {
                    int waitAmount = playerReader.GCD.Value;
                    if (waitAmount == 0)
                    {
                        waitAmount = MIN_GCD - SpellQueueTimeMs;
                    }
                    (stepbackTimeOut, stepbackElapsedMs) = wait.Until(waitAmount, () => prevState != interrupt());
                }
                else
                {
                    (stepbackTimeOut, stepbackElapsedMs) = wait.Until(item.StepBackAfterCast, () => prevState != interrupt());
                }

                if (item.Log)
                    LogStepBackAfterCastInterrupted(logger, item.Name, !stepbackTimeOut, stepbackElapsedMs);

                input.Proc.SetKeyState(input.Proc.BackwardKey, false);
            }

            UpdateSpellQueue(item, begin);

            item.ConsumeCharge();
            return true;
        }

        private bool WaitForGCD(KeyAction item, Func<bool> interrupt)
        {
            int totalTime = Math.Max(playerReader.GCD.Value, playerReader.RemainCastMs) - (SpellQueueTimeMs - playerReader.NetworkLatency.Value); //+ playerReader.NetworkLatency.Value
            if (totalTime < 0)
                return true;

            bool before = interrupt();
            (bool timeout, double elapsedMs) = wait.Until(totalTime,
                () => before != interrupt());

            if (item.Log)
                LogGCD(logger, item.Name, !timeout, addonReader.UsableAction.Is(item), totalTime, elapsedMs);

            return !timeout || before == interrupt();
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

            (bool changedTimeOut, double elapsedMs) = wait.Until(SpellQueueTimeMs + playerReader.NetworkLatency.Value, () => playerReader.Form == item.FormEnum);
            if (item.Log)
                LogFormChanged(logger, item.Name, !changedTimeOut, beforeForm.ToStringF(), playerReader.Form.ToStringF(), elapsedMs);

            classConfig.Form[index].SetClicked();

            return playerReader.Form == item.FormEnum;
        }

        private void UpdateSpellQueue(KeyAction item, DateTime begin)
        {
            int duration = Math.Max(playerReader.GCD.Value, playerReader.RemainCastMs)
                - SpellQueueTimeMs
                - (int)(DateTime.UtcNow - begin).TotalMilliseconds
                + playerReader.NetworkLatency.Value; /* - (2 * playerReader.NetworkLatency.Value)*/;

            SpellQueueOpen = DateTime.UtcNow.AddMilliseconds(duration);

            if (item.Log)
                LogWaitForGCD(logger, item.Name, playerReader.GCD.Value, playerReader.RemainCastMs, duration);
        }

        private void ReactToLastCastingEvent(KeyAction item, string source)
        {
            UI_ERROR value = (UI_ERROR)playerReader.CastEvent.Value;
            switch (value)
            {
                case UI_ERROR.NONE:
                case UI_ERROR.CAST_START:
                case UI_ERROR.CAST_SUCCESS:
                case UI_ERROR.SPELL_FAILED_TARGETS_DEAD:
                    break;
                case UI_ERROR.ERR_SPELL_FAILED_INTERRUPTED:
                    item.SetClicked();
                    break;
                case UI_ERROR.SPELL_FAILED_NOT_READY:
                /*
                int waitTime = Math.Max(playerReader.GCD.Value, playerReader.RemainCastMs);
                logger.LogInformation($"{source} React to {value.ToStringF()} -- wait for GCD {waitTime}ms");
                if (waitTime > 0)
                    wait.Fixed(waitTime);
                break;
                */
                case UI_ERROR.ERR_SPELL_COOLDOWN:
                    logger.LogInformation($"{source} React to {value.ToStringF()} -- wait until its ready");
                    int waitTime = Math.Max(playerReader.GCD.Value, playerReader.RemainCastMs);
                    bool before = addonReader.UsableAction.Is(item);
                    wait.Until(waitTime, () => before != addonReader.UsableAction.Is(item) || addonReader.UsableAction.Is(item));
                    break;
                case UI_ERROR.ERR_SPELL_FAILED_STUNNED:
                    int debuffCount = playerReader.AuraCount.PlayerDebuff;
                    if (debuffCount != 0)
                    {
                        logger.LogInformation($"{source} -- React to {value.ToStringF()} -- Wait till losing debuff!");
                        wait.While(() => debuffCount == playerReader.AuraCount.PlayerDebuff);
                    }
                    else
                    {
                        logger.LogInformation($"{source} -- Didn't know how to react {value.ToStringF()} when PlayerDebuffCount: {debuffCount}");
                    }

                    break;
                case UI_ERROR.ERR_SPELL_OUT_OF_RANGE:

                    if (!playerReader.Bits.HasTarget())
                        return;

                    if (playerReader.Class == PlayerClassEnum.Hunter && playerReader.IsInMeleeRange())
                    {
                        logger.LogInformation($"{source} -- As a Hunter didn't know how to react {value.ToStringF()}");
                        return;
                    }

                    float minRange = playerReader.MinRange();
                    if (bits.PlayerInCombat() && bits.HasTarget() && !playerReader.IsTargetCasting())
                    {
                        wait.Update();
                        wait.Update();
                        if (playerReader.TargetTarget == TargetTargetEnum.Me)
                        {
                            logger.LogInformation($"{source} -- React to {value.ToStringF()} -- Just wait for the target to get in range.");

                            (bool timeout, double elapsedMs) = wait.Until(GCD,
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

                            (bool timeout, double elapsedMs) = wait.Until(GCD,
                                () => minRange != playerReader.MinRange());

                            logger.LogInformation($"{source} -- React to {value.ToStringF()} -- Approached target {minRange}->{playerReader.MinRange()}");
                        }
                        else
                        {
                            logger.LogInformation($"{source} -- React to {value.ToStringF()} -- Start moving forward");
                            input.Proc.SetKeyState(input.Proc.ForwardKey, true);
                        }
                    }
                    break;
                case UI_ERROR.ERR_BADATTACKFACING:
                    if (playerReader.IsInMeleeRange())
                    {
                        logger.LogInformation($"{source} -- React to {value.ToStringF()} -- Interact!");
                        input.Interact();
                        stopMoving.Stop();
                    }
                    else
                    {
                        switch (playerReader.Class)
                        {
                            case PlayerClassEnum.None:
                                break;
                            case PlayerClassEnum.Monk:
                            case PlayerClassEnum.DemonHunter:
                            case PlayerClassEnum.Druid:
                            case PlayerClassEnum.DeathKnight:
                            case PlayerClassEnum.Warrior:
                            case PlayerClassEnum.Paladin:
                            case PlayerClassEnum.Rogue:
                                logger.LogInformation($"{source} -- React to {value.ToStringF()} -- Interact!");
                                input.Interact();
                                stopMoving.Stop();
                                break;
                            case PlayerClassEnum.Hunter:
                            case PlayerClassEnum.Priest:
                            case PlayerClassEnum.Shaman:
                            case PlayerClassEnum.Mage:
                            case PlayerClassEnum.Warlock:
                                stopMoving.Stop();
                                logger.LogInformation($"{source} -- React to {value.ToStringF()} -- Turning 180!");
                                float desiredDirection = playerReader.Direction + MathF.PI;
                                desiredDirection = desiredDirection > MathF.PI * 2 ? desiredDirection - (MathF.PI * 2) : desiredDirection;
                                direction.SetDirection(desiredDirection, Vector3.Zero);
                                break;
                        }

                        wait.Update();
                    }
                    break;
                case UI_ERROR.SPELL_FAILED_MOVING:
                    logger.LogInformation($"{source} -- React to {value.ToStringF()} -- Stop moving!");
                    wait.While(playerReader.Bits.IsFalling);
                    stopMoving.Stop();
                    wait.Update();
                    break;
                case UI_ERROR.ERR_SPELL_FAILED_ANOTHER_IN_PROGRESS:
                    logger.LogInformation($"{source} -- React to {value.ToStringF()} -- Wait till casting!");
                    wait.While(playerReader.IsCasting);
                    break;
                case UI_ERROR.ERR_BADATTACKPOS:
                    if (bits.SpellOn_AutoAttack())
                    {
                        logger.LogInformation($"{source} -- React to {value.ToStringF()} -- Interact!");
                        input.Interact();
                        stopMoving.Stop();
                        wait.Update();
                    }
                    else
                    {
                        logger.LogInformation($"{source} -- Didn't know how to React to {value.ToStringF()}");
                    }
                    break;
                default:
                    logger.LogInformation($"{source} -- Didn't know how to React to {value.ToStringF()}");
                    break;
            }
        }

        private void RepeatPetAttack()
        {
            if (bits.PlayerInCombat() &&
                bits.HasPet() &&
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
            Message = "[{name}] instant input {pressTime} {register} {inputElapsedMs}ms")]
        static partial void LogInstantInput(ILogger logger, string name, int pressTime, bool register, double inputElapsedMs);

        [LoggerMessage(
            EventId = 73,
            Level = LogLevel.Information,
            Message = "[{name}] ... usable: {beforeUsable}->{afterUsable} -- {beforeCastEvent}->{afterCastEvent} -- GCD: {gcd}")]
        static partial void LogInstantUsableChange(ILogger logger, string name, bool beforeUsable, bool afterUsable, string beforeCastEvent, string afterCastEvent, int gcd);

        [LoggerMessage(
            EventId = 74,
            Level = LogLevel.Information,
            Message = "[{name}] ... castbar skip validation")]
        static partial void LogCastbarSkipValidation(ILogger logger, string name);

        [LoggerMessage(
            EventId = 75,
            Level = LogLevel.Information,
            Message = "[{name}] ... castbar input {pressTime}ms {register} {inputElapsedMs}ms")]
        static partial void LogCastbarInput(ILogger logger, string name, int pressTime, bool register, double inputElapsedMs);

        [LoggerMessage(
            EventId = 76,
            Level = LogLevel.Information,
            Message = "[{name}] ... casting: {casting} -- count:{castCount} -- usable: {beforeUsable}->{afterUsable} -- {beforeCastEvent}->{afterCastEvent}")]
        static partial void LogCastbarUsableChange(ILogger logger, string name, bool casting, int castCount, bool beforeUsable, bool afterUsable, string beforeCastEvent, string afterCastEvent);

        [LoggerMessage(
            EventId = 77,
            Level = LogLevel.Information,
            Message = "[{name}] waiting for visible cast bar finish {remain}ms or interrupt...")]
        static partial void LogVisibleCastbarWaitForEnd(ILogger logger, string name, int remain);

        [LoggerMessage(
            EventId = 78,
            Level = LogLevel.Warning,
            Message = "[{name}] ... visible castbar interrupted!")]
        static partial void LogVisibleCastbarInterrupted(ILogger logger, string name);

        [LoggerMessage(
            EventId = 79,
            Level = LogLevel.Information,
            Message = "[{name}] waiting for hidden cast bar finish {remain}ms or interrupt...")]
        static partial void LogHiddenCastbarWaitForEnd(ILogger logger, string name, int remain);

        [LoggerMessage(
            EventId = 80,
            Level = LogLevel.Warning,
            Message = "[{name}] ... hidden castbar interrupted!")]
        static partial void LogHiddenCastbarInterrupted(ILogger logger, string name);


        [LoggerMessage(
            EventId = 81,
            Level = LogLevel.Warning,
            Message = "[{name}] ... after {before}->{after} form switch still not usable!")]
        static partial void LogAfterFormSwitchNotUsable(ILogger logger, string name, string before, string after);

        [LoggerMessage(
            EventId = 82,
            Level = LogLevel.Information,
            Message = "[{name}] DelayBeforeCast {delayBeforeCast}ms")]
        static partial void LogDelayBeforeCast(ILogger logger, string name, int delayBeforeCast);

        [LoggerMessage(
            EventId = 83,
            Level = LogLevel.Information,
            Message = "[{name}] ... AfterCastWaitBuff | Buff: {changeTimeOut} | {auraCount} | {elapsedMs}ms")]
        static partial void LogAfterCastWaitBuff(ILogger logger, string name, bool changeTimeOut, string auraCount, double elapsedMs);

        [LoggerMessage(
            EventId = 84,
            Level = LogLevel.Information,
            Message = "[{name}] ... DelayUntilCombat ? {success} {delayAfterCast}ms")]
        static partial void LogDelayUntilCombat(ILogger logger, string name, bool success, double delayAfterCast);

        [LoggerMessage(
            EventId = 85,
            Level = LogLevel.Information,
            Message = "[{name}] ... DelayAfterCast {delayAfterCast}ms")]
        static partial void LogDelayAfterCast(ILogger logger, string name, int delayAfterCast);

        [LoggerMessage(
            EventId = 86,
            Level = LogLevel.Information,
            Message = "[{name}] ... DelayAfterCast interrupted {delayElaspedMs}ms")]
        static partial void LogDelayAfterCastInterrupted(ILogger logger, string name, double delayElaspedMs);

        [LoggerMessage(
            EventId = 87,
            Level = LogLevel.Information,
            Message = "[{name}] ... instant interrupt: {interrupt} | CanRun: {canRun} | {canRunElapsedMs}ms")]
        static partial void LogWaitForInGameFeedback(ILogger logger, string name, bool interrupt, bool canRun, double canRunElapsedMs);

        [LoggerMessage(
            EventId = 88,
            Level = LogLevel.Information,
            Message = "[{name}] StepBackAfterCast {stepBackAfterCast}ms")]
        static partial void LogStepBackAfterCast(ILogger logger, string name, int stepBackAfterCast);

        [LoggerMessage(
            EventId = 89,
            Level = LogLevel.Information,
            Message = "[{name}] .... StepBackAfterCast interrupt: {interrupt} | {stepbackElapsedMs}ms")]
        static partial void LogStepBackAfterCastInterrupted(ILogger logger, string name, bool interrupt, double stepbackElapsedMs);

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
            Message = "[{name}] ... AfterCastWaitItem ? {changeTimeOut} | {elapsedMs}ms")]
        static partial void LogAfterCastWaitItem(ILogger logger, string name, bool changeTimeOut, double elapsedMs);

        [LoggerMessage(
            EventId = 93,
            Level = LogLevel.Information,
            Message = "[{name}] GCD: {gcd}ms | castRem: {remainCastMs} | Next Spell can be queued after {duration}ms")]
        static partial void LogWaitForGCD(ILogger logger, string name, int gcd, int remainCastMs, double duration);

        #endregion
    }
}