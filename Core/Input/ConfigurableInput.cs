using Game;

using Microsoft.Extensions.Logging;

using SharedLib;
using SharedLib.Humanization;

using System;
using System.Threading;

namespace Core;

public sealed partial class ConfigurableInput
{
    private readonly ILogger<ConfigurableInput> logger;
    private readonly WowProcessInput input;
    private readonly ClassConfiguration classConfig;
    private readonly AddonBits bits;
    private readonly IHumanizationProvider? humanization;
    private DateTime lastChatInputWarningUtc = DateTime.MinValue;
    private DateTime _lastClearTargetAttemptUtc = DateTime.MinValue;

    private readonly bool Log;

    public ConfigurableInput(ILogger<ConfigurableInput> logger,
        WowProcessInput input,
        ClassConfiguration classConfig,
        AddonBits bits,
        IHumanizationProvider? humanization = null)
    {
        this.logger = logger;
        this.input = input;
        this.classConfig = classConfig;
        this.bits = bits;
        this.humanization = humanization;
        Log = classConfig.Log;

        input.ForwardKey = classConfig.ForwardKey;
        input.BackwardKey = classConfig.BackwardKey;
        input.TurnLeftKey = classConfig.TurnLeftKey;
        input.TurnRightKey = classConfig.TurnRightKey;

        input.InteractMouseover = classConfig.InteractMouseOver.ConsoleKey;
        input.InteractMouseoverModifier = classConfig.InteractMouseOver.Modifier;
        input.InteractMouseoverPress = classConfig.InteractMouseOver.PressDuration;
    }

    public void Reset() => input.Reset();

    public void StartForward(bool forced)
    {
        if (!EnsureChatInputClosed())
        {
            return;
        }

        input.SetKeyState(ForwardKey, true, forced);
    }

    public void StopForward(bool forced)
    {
        if (input.IsKeyDown(ForwardKey))
            input.SetKeyState(ForwardKey, false, forced);
    }

    public void StartBackward(bool forced)
    {
        if (!EnsureChatInputClosed())
        {
            return;
        }

        input.SetKeyState(BackwardKey, true, forced);
    }

    public void StopBackward(bool forced)
    {
        if (input.IsKeyDown(BackwardKey))
            input.SetKeyState(BackwardKey, false, forced);
    }

    public void SetKeyState(ConsoleKey key, bool state, bool forced)
    {
        if (state && key != ConsoleKey.Escape && !EnsureChatInputClosed())
        {
            return;
        }

        input.SetKeyState(key, state, forced);
    }

    public void TurnRandomDir(int milliseconds, CancellationToken token = default)
    {
        input.PressRandom(
            Random.Shared.Next(2) == 0
            ? input.TurnLeftKey
            : input.TurnRightKey, milliseconds, token);
    }

    public int PressRandom(KeyAction keyAction, CancellationToken token = default)
    {
        if (keyAction.ConsoleKey != ConsoleKey.Escape &&
            !EnsureChatInputClosed(token))
        {
            return 0;
        }

        int elapsedMs;

        if (humanization?.Enabled == true && !humanization.IsOnBreak)
        {
            int reactionDelay = humanization.GetPreActionReactionDelayMs(complexity: 1, isMovementAction: false);
            if (reactionDelay > 0)
            {
                token.WaitHandle.WaitOne(reactionDelay);
                if (token.IsCancellationRequested)
                {
                    return 0;
                }
            }
        }

        // Use modifier-aware pressing if the keyAction has a modifier
        if (keyAction.HasModifier)
        {
            elapsedMs = input.PressRandomWithModifier(keyAction.ConsoleKey, keyAction.Modifier, keyAction.PressDuration, token);
        }
        else
        {
            elapsedMs = input.PressRandom(keyAction.ConsoleKey, keyAction.PressDuration, token);
        }

        keyAction.SetClicked();

        if (Log && keyAction.Log)
        {
            if (keyAction.BaseAction)
                LogBaseActionPressRandom(logger, keyAction.Name, keyAction.ConsoleKey, keyAction.Modifier.ToPrefix(), elapsedMs);
            else
                LogKeyActionPressRandom(logger, keyAction.Name, keyAction.ConsoleKey, keyAction.Modifier.ToPrefix(), elapsedMs);
        }

        return elapsedMs;
    }

    public void PressFixed(ConsoleKey key, int milliseconds, CancellationToken token)
    {
        if (key != ConsoleKey.Escape &&
            !EnsureChatInputClosed(token))
        {
            return;
        }

        input.PressFixed(key, milliseconds, token);
    }

    public void PressRandom(ConsoleKey key, int milliseconds)
    {
        if (key != ConsoleKey.Escape &&
            !EnsureChatInputClosed())
        {
            return;
        }

        input.PressRandom(key, milliseconds);
    }

    public bool IsKeyDown(ConsoleKey key) => input.IsKeyDown(key);

    public void PressInteract(CancellationToken token = default) => PressRandom(Interact, token);

    public void RightClickCurrentMouse()
    {
        if (!EnsureChatInputClosed())
        {
            return;
        }

        input.RightClickCurrent();
    }

    public void LeftClickCurrentMouse()
    {
        if (!EnsureChatInputClosed())
        {
            return;
        }

        input.LeftClickCurrent();
    }

    public void PressFastInteract(CancellationToken token = default)
    {
        if (!EnsureChatInputClosed(token))
        {
            return;
        }

        if (Interact.HasModifier)
            input.PressRandomWithModifier(Interact.ConsoleKey, Interact.Modifier, InputDuration.FastPress, token);
        else
            input.PressRandom(Interact.ConsoleKey, InputDuration.FastPress, token);
        Interact.SetClicked();
    }

    public void PressVeryFastInteract()
    {
        if (!EnsureChatInputClosed())
        {
            return;
        }

        if (Interact.HasModifier)
            input.PressRandomWithModifier(Interact.ConsoleKey, Interact.Modifier, InputDuration.VeryFastPress);
        else
            input.PressRandom(Interact.ConsoleKey, InputDuration.VeryFastPress);
        Interact.SetClicked();
    }

    public void PressApproachOnCooldown()
    {
        if (Approach.OnCooldown())
        {
            return;
        }

        if (!EnsureChatInputClosed())
        {
            return;
        }

        if (Approach.HasModifier)
            input.PressRandomWithModifier(Approach.ConsoleKey, Approach.Modifier, InputDuration.FastPress);
        else
            input.PressRandom(Approach.ConsoleKey, InputDuration.FastPress);
        Approach.SetClicked();
    }

    public bool PressedApproachOnCooldown()
    {
        if (Approach.OnCooldown())
        {
            return false;
        }

        if (!EnsureChatInputClosed())
        {
            return false;
        }

        if (Approach.HasModifier)
            input.PressRandomWithModifier(Approach.ConsoleKey, Approach.Modifier, InputDuration.FastPress);
        else
            input.PressRandom(Approach.ConsoleKey, InputDuration.FastPress);
        Approach.SetClicked();
        return true;
    }

    public void PressApproach(CancellationToken token = default) => PressRandom(Approach, token);

    public void PressLastTarget(CancellationToken token = default) => PressRandom(TargetLastTarget, token);

    /// <summary>
    /// Presses TargetLastTarget and waits for a target to appear.
    /// </summary>
    /// <returns>True if target appeared within timeout, false if timed out.</returns>
    public bool PressLastTargetAndWait(Wait wait, Func<bool> hasTarget, int timeoutMs = 300, CancellationToken token = default)
    {
        PressLastTarget(token);
        return wait.Until(timeoutMs, hasTarget) > 0;
    }

    public void PressFastLastTarget(CancellationToken token = default)
    {
        if (!EnsureChatInputClosed(token))
        {
            return;
        }

        if (TargetLastTarget.HasModifier)
            input.PressRandomWithModifier(TargetLastTarget.ConsoleKey, TargetLastTarget.Modifier, InputDuration.FastPress, token);
        else
            input.PressRandom(TargetLastTarget.ConsoleKey, InputDuration.FastPress, token);
        TargetLastTarget.SetClicked();
    }

    /// <summary>
    /// Presses TargetLastTarget (fast) and waits for a target to appear.
    /// </summary>
    /// <returns>True if target appeared within timeout, false if timed out.</returns>
    public bool PressFastLastTargetAndWait(Wait wait, Func<bool> hasTarget, int timeoutMs = 300, CancellationToken token = default)
    {
        PressFastLastTarget(token);
        return wait.Until(timeoutMs, hasTarget) > 0;
    }

    public void PressStandUp(CancellationToken token = default) => PressRandom(StandUp, token);

    public void PressClearTarget(CancellationToken token = default)
    {
        PressRandom(ClearTarget, token);
    }

    public void PressF11ClearTarget(CancellationToken token = default)
    {
        logger.LogDebug("[PressClearTarget ] F11 macro key pressed");
        input.PressRandom(ConsoleKey.F11, InputDuration.FastPress, token);
    }

    /// <summary>
    /// Aggressively clears target with escalating fallback path.
    /// Returns true when the target no longer exists after any stage.
    /// </summary>
    public bool ForceAggressiveClearTarget(
        Wait wait,
        AddonBits bits,
        ExecGameCommand? execGameCommand = null,
        CancellationToken token = default)
    {
        // Rate-limit: avoid spamming ClearTarget when called on every tick.
        if ((DateTime.UtcNow - _lastClearTargetAttemptUtc).TotalMilliseconds < 500)
            return false;
        _lastClearTargetAttemptUtc = DateTime.UtcNow;

        // Stage 1: Configured binding retries.
        if (ClearTarget.ConsoleKey != ConsoleKey.NoName)
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                PressRandom(ClearTarget, token);
                wait.Update();
                if (!bits.Target())
                {
                    logger.LogInformation("[ClearTarget      ] Cleared via configured binding (attempt {Attempt})", attempt);
                    return true;
                }
            }
        }

        // Stage 2: Escape key fallback.
        PressESC(token);
        wait.Update();
        if (!bits.Target())
        {
            logger.LogInformation("[ClearTarget      ] Cleared via ESC");
            return true;
        }

        // Stage 3: F11 macro retries.
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            PressF11ClearTarget(token);
            wait.Update();
            if (!bits.Target())
            {
                logger.LogInformation("[ClearTarget      ] Cleared via F11 (attempt {Attempt})", attempt);
                return true;
            }
        }

        // Stage 4: Slash command fallback when available.
        if (execGameCommand != null)
        {
            execGameCommand.Run("/cleartarget", logMessage: null);
            wait.Update();
            if (!bits.Target())
            {
                logger.LogInformation("[ClearTarget      ] Cleared via /cleartarget command");
                return true;
            }
        }

        logger.LogError("[ClearTarget      ] FAILED: All target clearing methods exhausted; target persists.");
        return false;
    }

    public void PressStopAttack(CancellationToken token = default) => PressRandom(StopAttack, token);

    /// <summary>
    /// Attempts to stop auto-attack with verification.
    /// Presses StopAttack key and waits for combat state to clear.
    /// </summary>
    /// <returns>True if combat stopped within timeout, false otherwise.</returns>
    public bool ForceStopAttack(Wait wait, AddonBits bits, int timeoutMs = 500, CancellationToken token = default)
    {
        // Press stop attack
        PressStopAttack(token);

        // Wait for combat to clear or auto-attack to stop
        return wait.Until(timeoutMs, () => !bits.Combat() && !bits.Any_AutoAttack()) > 0;
    }

    public void PressNearestTarget(CancellationToken token = default) => PressRandom(TargetNearestTarget, token);

    /// <summary>
    /// Presses TargetNearestTarget and waits for a target to appear.
    /// </summary>
    /// <returns>True if target appeared within timeout, false if timed out.</returns>
    public bool PressNearestTargetAndWait(Wait wait, Func<bool> hasTarget, int timeoutMs = 300, CancellationToken token = default)
    {
        PressNearestTarget(token);
        return wait.Until(timeoutMs, hasTarget) > 0;
    }

    public void PressTargetPet(CancellationToken token = default) => PressRandom(TargetPet, token);

    public void PressTargetOfTarget(CancellationToken token = default) => PressRandom(TargetTargetOfTarget, token);

    public void PressJump(CancellationToken token = default) => PressRandom(Jump, token);

    public void PressPetAttack(CancellationToken token = default) => PressRandom(PetAttack, token);

    public void PressMount(CancellationToken token = default) => PressRandom(Mount, token);

    /// <summary>
    /// Attempts to mount with verification.
    /// Presses Mount key and waits for mounted state to appear.
    /// </summary>
    /// <returns>True if mounted within timeout, false otherwise.</returns>
    public bool PressMountAndWait(Wait wait, AddonBits bits, int timeoutMs = 2000, CancellationToken token = default)
    {
        PressMount(token);
        return wait.Until(timeoutMs, bits.Mounted) > 0;
    }

    public void PressDismount(CancellationToken token = default)
    {
        if (!EnsureChatInputClosed(token))
        {
            return;
        }

        if (Mount.HasModifier)
            input.PressRandomWithModifier(Mount.ConsoleKey, Mount.Modifier, Mount.PressDuration, token);
        else
            input.PressRandom(Mount.ConsoleKey, Mount.PressDuration, token);
    }

    public void PressTargetFocus(CancellationToken token = default) => PressRandom(TargetFocus, token);

    public void PressFollowTarget(CancellationToken token = default) => PressRandom(FollowTarget, token);

    public void PressESC(CancellationToken token = default)
    {
        input.PressRandom(ConsoleKey.Escape, InputDuration.VeryFastPress, token);
    }

    private bool EnsureChatInputClosed(CancellationToken token = default)
    {
        if (!bits.ChatInputIsVisible())
        {
            return true;
        }

        DateTime now = DateTime.UtcNow;
        if ((now - lastChatInputWarningUtc).TotalSeconds >= 2)
        {
            logger.LogWarning("[ConfigurableInput] Chat input is visible; pressing ESC to prevent key spam");
            lastChatInputWarningUtc = now;
        }

        PressESC(token);
        return !bits.ChatInputIsVisible();
    }

    #region Logging

    [LoggerMessage(
        EventId = 5000,
        Level = LogLevel.Trace,
        Message = @"[{name}] {modifierPrefix}{key} pressed {milliseconds}ms")]
    static partial void LogBaseActionPressRandom(ILogger logger, string name, ConsoleKey key, string modifierPrefix, int milliseconds);

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Debug,
        Message = @"[{name}] {modifierPrefix}{key} pressed {milliseconds}ms")]
    static partial void LogKeyActionPressRandom(ILogger logger, string name, ConsoleKey key, string modifierPrefix, int milliseconds);

    #endregion
}
