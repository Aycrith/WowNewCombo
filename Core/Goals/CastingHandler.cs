using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;

namespace Core.Goals
{
    public class CastingHandler : IDisposable
    {
        private readonly ILogger logger;
        private readonly ConfigurableInput input;

        private readonly Wait wait;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;

        private readonly ClassConfiguration classConfig;
        private readonly PlayerDirection direction;
        private readonly StopMoving stopMoving;

        private readonly KeyAction defaultKeyAction = new();

        private readonly Func<bool> defaultInterrupt;

        private const int GCD = 1500;
        private const int SpellQueueTimeMs = 400;

        private const int MaxWaitCastTimeMs = GCD;
        private const int MaxWaitBuffTimeMs = GCD;
        private const int MaxCastTimeMs = 15000;
        private const int MaxAirTimeMs = 10000;

        public CastingHandler(ILogger logger, ConfigurableInput input, Wait wait, AddonReader addonReader, ClassConfiguration classConfig, PlayerDirection direction, StopMoving stopMoving)
        {
            this.logger = logger;
            this.input = input;

            this.wait = wait;
            this.addonReader = addonReader;
            this.playerReader = addonReader.PlayerReader;

            this.classConfig = classConfig;
            this.direction = direction;
            this.stopMoving = stopMoving;

            defaultInterrupt = () => playerReader.HasTarget;
        }

        public void Dispose()
        {
            defaultKeyAction.Dispose();
        }

        public bool CanRun(KeyAction item)
        {
            if (item.School != SchoolMask.None &&
                classConfig.ImmunityBlacklist.TryGetValue(playerReader.TargetId, out var list) &&
                list.Contains(item.School))
            {
                return false;
            }

            return item.CanRun();
        }

        private void PressKeyAction(KeyAction item)
        {
            playerReader.LastUIErrorMessage = UI_ERROR.NONE;

            if (item.AfterCastWaitNextSwing)
            {
                if (item.Log)
                    item.LogInformation("wait for next swing!");
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
                wait.Update(1);
            }

            playerReader.CastEvent.ForceUpdate(0);
            int beforeCastEventValue = playerReader.CastEvent.Value;
            int beforeSpellId = playerReader.CastSpellId.Value;
            bool beforeUsable = addonReader.UsableAction.Is(item);

            PressKeyAction(item);

            if (item.SkipValidation)
            {
                if (item.Log)
                    item.LogInformation($" ... instant skip validation");
                return true;
            }

            bool inputTimeOut;
            double inputElapsedMs;

            if (item.AfterCastWaitNextSwing)
            {
                (inputTimeOut, inputElapsedMs) = wait.Until(playerReader.MainHandSpeedMs,
                    interrupt: () => !addonReader.CurrentAction.Is(item),
                    repeat: () =>
                    {
                        if (classConfig.Approach.GetCooldownRemaining() == 0)
                        {
                            input.Approach();
                        }
                    });
            }
            else
            {
                (inputTimeOut, inputElapsedMs) = wait.Until(MaxWaitCastTimeMs,
                    interrupt: () =>
                    (beforeSpellId != playerReader.CastSpellId.Value && beforeCastEventValue != playerReader.CastEvent.Value) ||
                    beforeUsable != addonReader.UsableAction.Is(item)
                );
            }

            if (!inputTimeOut)
            {
                if (item.Log)
                    item.LogInformation($" ... instant input {inputElapsedMs}ms");
            }
            else
            {
                if (item.Log)
                    item.LogInformation($" ... instant input not registered! {inputElapsedMs}ms");
                return false;
            }

            if (item.Log)
                item.LogInformation($" ... usable: {beforeUsable}->{addonReader.UsableAction.Is(item)} -- ({(UI_ERROR)beforeCastEventValue}->{(UI_ERROR)playerReader.CastEvent.Value})");

            if (!CastSuccessfull((UI_ERROR)playerReader.CastEvent.Value))
            {
                ReactToLastCastingEvent(item, $"{item.Name}-{nameof(CastingHandler)}: CastInstant");
                return false;
            }
            return true;
        }

        private bool CastCastbar(KeyAction item, Func<bool> interrupt)
        {
            if (playerReader.Bits.IsFalling)
            {
                (bool fallTimeOut, double fallElapsedMs) = wait.Until(MaxAirTimeMs, () => !playerReader.Bits.IsFalling);
                if (!fallTimeOut)
                {
                    if (item.Log)
                        item.LogInformation($" ... castbar waited for landing {fallElapsedMs}ms");
                }
            }

            if (playerReader.IsCasting && interrupt())
            {
                //if (item.Log) // really spammy
                //    item.LogInformation($" ... castbar during cast interrupted!");
                return false;
            }

            stopMoving.Stop();
            wait.Update(1);
            stopMoving.Stop();
            wait.Update(2);

            bool prevState = interrupt();

            bool beforeUsable = addonReader.UsableAction.Is(item);
            int beforeCastEventValue = playerReader.CastEvent.Value;
            int beforeSpellId = playerReader.CastSpellId.Value;
            int beforeCastCount = playerReader.CastCount;

            PressKeyAction(item);

            if (item.SkipValidation)
            {
                if (item.Log)
                    item.LogInformation($" ... castbar skip validation");
                return true;
            }

            (bool inputTimeOut, double inputElapsedMs) = wait.Until(MaxWaitCastTimeMs,
                interrupt: () =>
                beforeCastEventValue != playerReader.CastEvent.Value ||
                beforeSpellId != playerReader.CastSpellId.Value ||
                beforeCastCount != playerReader.CastCount
                );

            if (!inputTimeOut)
            {
                if (item.Log)
                    item.LogInformation($" ... castbar input {inputElapsedMs}ms");
            }
            else
            {
                if (item.Log)
                    item.LogInformation($" ... castbar input not registered! {inputElapsedMs}ms");
                return false;
            }
            if (item.Log)
                item.LogInformation($" ... casting: {playerReader.IsCasting} -- count:{playerReader.CastCount} -- usable: {beforeUsable}->{addonReader.UsableAction.Is(item)} -- {(UI_ERROR)beforeCastEventValue}->{(UI_ERROR)playerReader.CastEvent.Value}");

            if (!CastSuccessfull((UI_ERROR)playerReader.CastEvent.Value))
            {
                ReactToLastCastingEvent(item, $"{item.Name}-{nameof(CastingHandler)}: CastCastbar");
                return false;
            }

            if (playerReader.IsCasting)
            {
                if (item.Log)
                    item.LogInformation(" ... waiting for visible cast bar to end or interrupt.");
                wait.Until(MaxCastTimeMs, () => !playerReader.IsCasting || prevState != interrupt());
                if (prevState != interrupt())
                {
                    if (item.Log)
                        item.LogWarning(" ... visible castbar interrupted!");
                    return false;
                }
            }
            else if ((UI_ERROR)playerReader.CastEvent.Value == UI_ERROR.CAST_START)
            {
                beforeCastEventValue = playerReader.CastEvent.Value;
                if (item.Log)
                    item.LogInformation(" ... waiting for hidden cast bar to end or interrupt.");
                wait.Until(MaxCastTimeMs, () => beforeCastEventValue != playerReader.CastEvent.Value || prevState != interrupt());
                if (prevState != interrupt())
                {
                    if (item.Log)
                        item.LogWarning(" ... hidden castbar interrupted!");
                    return false;
                }
            }

            return true;
        }

        public bool CastIfReady(KeyAction item)
        {
            return CanRun(item) && Cast(item, defaultInterrupt);
        }

        public bool CastIfReady(KeyAction item, Func<bool> interrupt)
        {
            return CanRun(item) && Cast(item, interrupt);
        }

        public bool Cast(KeyAction item, Func<bool> interrupt)
        {
            if (item.HasFormRequirement() && playerReader.Form != item.FormEnum)
            {
                bool beforeUsable = addonReader.UsableAction.Is(item);
                var beforeForm = playerReader.Form;

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
                            item.LogInformation($" ... after switch {beforeForm}->{playerReader.Form} still not usable!");
                        return false;
                    }
                }
            }

            if (playerReader.Bits.IsAutoRepeatSpellOn_Shoot)
            {
                logger.LogInformation("Stop AutoRepeat Shoot");
                input.StopAttack();
                input.StopAttack();
                wait.Update(1);
            }

            if (item.DelayBeforeCast > 0)
            {
                if (item.StopBeforeCast || item.HasCastBar)
                {
                    stopMoving.Stop();
                    wait.Update(1);
                    stopMoving.Stop();
                    wait.Update(1);
                }

                if (item.Log)
                    item.LogInformation($" Wait {item.DelayBeforeCast}ms before press.");
                Thread.Sleep(item.DelayBeforeCast);
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
                    item.LogInformation($" ... AfterCastWaitBuff: Buff: {!changeTimeOut} | {playerReader.AuraCount} | Delay: {elapsedMs}ms");
            }

            if (item.DelayAfterCast != defaultKeyAction.DelayAfterCast)
            {
                if (item.DelayUntilCombat) // stop waiting if the mob is targetting me
                {
                    if (item.Log)
                        item.LogInformation($" ... DelayUntilCombat ... delay after cast {item.DelayAfterCast}ms");

                    var sw = new Stopwatch();
                    sw.Start();
                    while (sw.ElapsedMilliseconds < item.DelayAfterCast)
                    {
                        wait.Update(1);
                        if (playerReader.Bits.TargetOfTargetIsPlayer)
                        {
                            break;
                        }
                    }
                }
                else if (item.DelayAfterCast > 0)
                {
                    if (item.Log)
                        item.LogInformation($" ... delay after cast {item.DelayAfterCast}ms");
                    (bool delayTimeOut, double delayElaspedMs) = wait.Until(item.DelayAfterCast, () => prevState != interrupt());
                    if (item.Log)
                    {
                        if (!delayTimeOut)
                        {
                            item.LogInformation($" .... delay after cast interrupted {delayElaspedMs}ms");
                        }
                        else
                        {
                            item.LogInformation($" .... delay after cast not interrupted {delayElaspedMs}ms");
                        }
                    }
                }
            }
            else
            {
                if (item.RequirementObjects.Count > 0)
                {
                    (bool canRun, double canRunElapsedMs) = wait.Until(SpellQueueTimeMs,
                        () => !item.CanRun()
                    );
                    if (item.Log)
                        item.LogInformation($" ... instant interrupt: {!canRun} | CanRun: {item.CanRun()} | Delay: {canRunElapsedMs}ms");
                }
            }

            if (item.StepBackAfterCast > 0)
            {
                if (item.Log)
                    item.LogInformation($"Step back for {item.StepBackAfterCast}ms");
                input.SetKeyState(input.BackwardKey, true);
                (bool stepbackTimeOut, double stepbackElapsedMs) =
                    wait.Until(item.StepBackAfterCast, () => prevState != interrupt());
                if (!stepbackTimeOut)
                {
                    if (item.Log)
                        item.LogInformation($" .... interrupted stepback | interrupted? {prevState != interrupt()} | {stepbackElapsedMs}ms");
                }
                input.SetKeyState(input.BackwardKey, false);
            }

            if (item.AfterCastWaitNextSwing)
            {
                wait.Update(1);
            }

            item.ConsumeCharge();
            return true;
        }

        private bool WaitForGCD(KeyAction item, Func<bool> interrupt)
        {
            bool before = interrupt();
            (bool timeout, double elapsedMs) = wait.Until(GCD,
                () => addonReader.UsableAction.Is(item) || before != interrupt());
            if (!timeout)
            {
                //item.LogInformation($" ... gcd interrupted {elapsedMs}ms");
                if (before != interrupt())
                {
                    if (item.Log)
                        item.LogInformation($" ... gcd interrupted! interrupt: {before} -> {interrupt()}");
                    return false;
                }
            }
            else
            {
                if (item.Log)
                    item.LogInformation($" ... gcd fully waited {elapsedMs}ms");
            }

            return true;
        }

        public bool SwitchForm(Form beforeForm, KeyAction item)
        {
            int index = classConfig.Form.FindIndex(x => x.FormEnum == item.FormEnum);
            if (index == -1)
            {
                logger.LogWarning($"Unable to find Key in ClassConfig.Form to transform into {item.FormEnum}");
                return false;
            }

            PressKeyAction(classConfig.Form[index]);

            (bool changedTimeOut, double elapsedMs) = wait.Until(SpellQueueTimeMs, () => playerReader.Form == item.FormEnum);
            if (item.Log)
                item.LogInformation($" ... form changed: {!changedTimeOut} | {beforeForm} -> {playerReader.Form} | Delay: {elapsedMs}ms");

            return playerReader.Form == item.FormEnum;
        }

        public void ReactToLastUIErrorMessage(string source)
        {
            //var lastError = playerReader.LastUIErrorMessage;
            switch (playerReader.LastUIErrorMessage)
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
                        logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_SPELL_FAILED_STUNNED} -- Wait till losing debuff!");
                        wait.While(() => debuffCount == playerReader.AuraCount.PlayerDebuff);

                        wait.Update(1);
                        playerReader.LastUIErrorMessage = UI_ERROR.NONE;
                    }
                    else
                    {
                        logger.LogInformation($"{source} -- Didn't know how to react {UI_ERROR.ERR_SPELL_FAILED_STUNNED} when PlayerDebuffCount: {debuffCount}");
                    }
                    break;
                case UI_ERROR.ERR_SPELL_OUT_OF_RANGE:
                    if (playerReader.Class == PlayerClassEnum.Hunter && playerReader.IsInMeleeRange)
                    {
                        logger.LogInformation($"{source} -- As a Hunter didn't know how to react {UI_ERROR.ERR_SPELL_OUT_OF_RANGE}");
                        return;
                    }

                    logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_SPELL_OUT_OF_RANGE} -- Face enemy and start moving forward");
                    input.Interact();
                    input.SetKeyState(input.ForwardKey, true);

                    wait.Update(1);
                    playerReader.LastUIErrorMessage = UI_ERROR.NONE;
                    break;
                case UI_ERROR.ERR_BADATTACKFACING:

                    if (playerReader.IsInMeleeRange)
                    {
                        logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_BADATTACKFACING} -- Interact!");
                        input.Interact();
                    }
                    else
                    {
                        logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_BADATTACKFACING} -- Turning 180!");

                        float desiredDirection = playerReader.Direction + MathF.PI;
                        desiredDirection = desiredDirection > MathF.PI * 2 ? desiredDirection - (MathF.PI * 2) : desiredDirection;
                        direction.SetDirection(desiredDirection, Vector3.Zero);
                    }

                    wait.Update(1);
                    playerReader.LastUIErrorMessage = UI_ERROR.NONE;
                    break;
                case UI_ERROR.SPELL_FAILED_MOVING:
                    logger.LogInformation($"{source} -- React to {UI_ERROR.SPELL_FAILED_MOVING} -- Stop moving!");

                    stopMoving.Stop();
                    wait.Update(1);
                    playerReader.LastUIErrorMessage = UI_ERROR.NONE;
                    break;
                case UI_ERROR.ERR_SPELL_FAILED_ANOTHER_IN_PROGRESS:
                    logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_SPELL_FAILED_ANOTHER_IN_PROGRESS} -- Wait till casting!");
                    wait.While(() => playerReader.IsCasting);

                    wait.Update(1);
                    playerReader.LastUIErrorMessage = UI_ERROR.NONE;
                    break;
                case UI_ERROR.ERR_SPELL_COOLDOWN:
                    logger.LogInformation($"{source} -- Cant react to {UI_ERROR.ERR_SPELL_FAILED_ANOTHER_IN_PROGRESS}");

                    wait.Update(1);
                    playerReader.LastUIErrorMessage = UI_ERROR.NONE;
                    break;
                case UI_ERROR.ERR_BADATTACKPOS:
                    if (playerReader.Bits.IsAutoRepeatSpellOn_AutoAttack)
                    {
                        logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_BADATTACKPOS} -- Interact!");
                        input.Interact();
                        stopMoving.Stop();
                        wait.Update(1);

                        playerReader.LastUIErrorMessage = UI_ERROR.NONE;
                    }
                    else
                    {
                        logger.LogInformation($"{source} -- Didn't know how to React to {playerReader.LastUIErrorMessage}");
                    }
                    break;
                default:
                    logger.LogInformation($"{source} -- Didn't know how to React to {playerReader.LastUIErrorMessage}");
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
                    logger.LogInformation($"{source} React to {UI_ERROR.ERR_SPELL_COOLDOWN} -- wait until its ready");
                    bool before = addonReader.UsableAction.Is(item);
                    wait.While(() => before != addonReader.UsableAction.Is(item));

                    break;
                case UI_ERROR.ERR_SPELL_FAILED_STUNNED:
                    int debuffCount = playerReader.AuraCount.PlayerDebuff;
                    if (debuffCount != 0)
                    {
                        logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_SPELL_FAILED_STUNNED} -- Wait till losing debuff!");
                        wait.While(() => debuffCount == playerReader.AuraCount.PlayerDebuff);
                    }
                    else
                    {
                        logger.LogInformation($"{source} -- Didn't know how to react {UI_ERROR.ERR_SPELL_FAILED_STUNNED} when PlayerDebuffCount: {debuffCount}");
                    }

                    break;
                case UI_ERROR.ERR_SPELL_OUT_OF_RANGE:
                    if (playerReader.Class == PlayerClassEnum.Hunter && playerReader.IsInMeleeRange)
                    {
                        logger.LogInformation($"{source} -- As a Hunter didn't know how to react {UI_ERROR.ERR_SPELL_OUT_OF_RANGE}");
                        return;
                    }

                    float minRange = playerReader.MinRange;
                    if (playerReader.Bits.PlayerInCombat && playerReader.HasTarget && !playerReader.IsTargetCasting)
                    {
                        wait.Update(2);
                        if (playerReader.TargetTarget == TargetTargetEnum.Me)
                        {
                            logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_SPELL_OUT_OF_RANGE} -- Just wait for the target to get in range.");
                            
                            (bool timeout, double elapsedMs) = wait.Until(MaxWaitCastTimeMs,
                                () => minRange != playerReader.MinRange || playerReader.IsTargetCasting
                            );
                        }
                    }
                    else
                    {
                        double beforeDirection = playerReader.Direction;
                        input.Interact();
                        input.StopAttack();
                        stopMoving.Stop();
                        wait.Update(1);

                        if (beforeDirection != playerReader.Direction)
                        {
                            input.Interact();

                            (bool timeout, double elapsedMs) = wait.Until(MaxWaitCastTimeMs,
                                () => minRange != playerReader.MinRange);

                            logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_SPELL_OUT_OF_RANGE} -- Approached target {minRange}->{playerReader.MinRange}");
                        }
                        else
                        {
                            logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_SPELL_OUT_OF_RANGE} -- Start moving forward");
                            input.SetKeyState(input.ForwardKey, true);
                        }


                    }

                    break;
                case UI_ERROR.ERR_BADATTACKFACING:
                    if (playerReader.IsInMeleeRange)
                    {
                        logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_BADATTACKFACING} -- Interact!");
                        input.Interact();
                    }
                    else
                    {
                        logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_BADATTACKFACING} -- Turning 180!");

                        float desiredDirection = playerReader.Direction + MathF.PI;
                        desiredDirection = desiredDirection > MathF.PI * 2 ? desiredDirection - (MathF.PI * 2) : desiredDirection;
                        direction.SetDirection(desiredDirection, Vector3.Zero);

                        wait.Update(1);
                    }

                    break;
                case UI_ERROR.SPELL_FAILED_MOVING:
                    logger.LogInformation($"{source} -- React to {UI_ERROR.SPELL_FAILED_MOVING} -- Stop moving!");
                    stopMoving.Stop();
                    wait.Update(1);

                    break;
                case UI_ERROR.ERR_SPELL_FAILED_ANOTHER_IN_PROGRESS:
                    logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_SPELL_FAILED_ANOTHER_IN_PROGRESS} -- Wait till casting!");
                    wait.While(() => playerReader.IsCasting);

                    break;
                case UI_ERROR.ERR_BADATTACKPOS:
                    if (playerReader.Bits.IsAutoRepeatSpellOn_AutoAttack)
                    {
                        logger.LogInformation($"{source} -- React to {UI_ERROR.ERR_BADATTACKPOS} -- Interact!");
                        input.Interact();
                        stopMoving.Stop();
                        wait.Update(1);
                    }
                    else
                    {
                        logger.LogInformation($"{source} -- Didn't know how to React to {(UI_ERROR)playerReader.CastEvent.Value}");
                    }

                    break;
                default:
                    logger.LogInformation($"{source} -- Didn't know how to React to {(UI_ERROR)playerReader.CastEvent.Value}");

                    break;
                    //case UI_ERROR.ERR_SPELL_FAILED_S:
                    //case UI_ERROR.ERR_BADATTACKPOS:
                    //case UI_ERROR.ERR_SPELL_OUT_OF_RANGE:
                    //case UI_ERROR.ERR_AUTOFOLLOW_TOO_FAR:
                    //    this.playerReader.LastUIErrorMessage = UI_ERROR.NONE;
                    //    break;
            }
        }
    }
}