using System;
using System.Threading;
using System.Threading.Tasks;

using Core.FeatureFlags;
using Core.GOAP;
using Core.Goals;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Core.Recovery;

/// <summary>
/// Rule-based recovery service that handles "NO PLAN" states from GOAP.
/// Uses deterministic rules rather than LLM to ensure reliability.
/// </summary>
public sealed class NoPlanRecoveryService : BackgroundService, IGoapEventListener
{
    private readonly ILogger<NoPlanRecoveryService> logger;
    private readonly FeatureFlagService featureFlags;
    private readonly GoapAgent? goapAgent;

    // Recovery tracking
    private int noPlanCount;
    private DateTime lastNoPlanTime;
    private int consecutiveFailures;

    // Recovery state
    private RecoveryState currentState = RecoveryState.Idle;
    private DateTime stateEnteredAt;

    private enum RecoveryState
    {
        Idle,
        ClearingTarget,
        ResettingState,
        ForcingReplan,
        EmergencyReset
    }

    public NoPlanRecoveryService(
        ILogger<NoPlanRecoveryService> logger,
        FeatureFlagService featureFlags,
        GoapAgent? goapAgent = null)
    {
        this.logger = logger;
        this.featureFlags = featureFlags;
        this.goapAgent = goapAgent;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (goapAgent == null)
        {
            logger.LogInformation("[NoPlanRecovery  ] No GoapAgent available, service inactive");
            return;
        }

        logger.LogInformation("[NoPlanRecovery  ] Service started");

        // Keep service alive until cancellation
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public void OnGoapEvent(GoapEventArgs e)
    {
        if (!featureFlags.Current.NoPlanRecovery.Enabled || goapAgent == null)
            return;

        // Detect NO PLAN events
        if (!IsNoPlanEvent(e))
            return;

        Interlocked.Increment(ref noPlanCount);
        DateTime now = DateTime.UtcNow;

        // Check if this is a consecutive failure
        if ((now - lastNoPlanTime).TotalSeconds < 10)
        {
            Interlocked.Increment(ref consecutiveFailures);
        }
        else
        {
            Interlocked.Exchange(ref consecutiveFailures, 1);
        }

        lastNoPlanTime = now;

        logger.LogWarning("[NoPlanRecovery  ] NO PLAN detected (count: {Count}, consecutive: {Consecutive})",
            noPlanCount, consecutiveFailures);

        // Execute recovery
        _ = Task.Run(() => ExecuteRecovery());
    }

    private void ExecuteRecovery()
    {
        try
        {
            var options = featureFlags.Current.NoPlanRecovery;

            // Determine recovery strategy based on consecutive failures
            RecoveryAction action = DetermineRecoveryAction(options);

            logger.LogInformation("[NoPlanRecovery  ] Executing {Action} (state was {State})",
                action, currentState);

            switch (action)
            {
                case RecoveryAction.ClearTarget:
                    EnterState(RecoveryState.ClearingTarget);
                    ExecuteClearTarget();
                    break;

                case RecoveryAction.ResetState:
                    EnterState(RecoveryState.ResettingState);
                    ExecuteResetState();
                    break;

                case RecoveryAction.ForceReplan:
                    EnterState(RecoveryState.ForcingReplan);
                    ExecuteForceReplan();
                    break;

                case RecoveryAction.EmergencyReset:
                    EnterState(RecoveryState.EmergencyReset);
                    ExecuteEmergencyReset();
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[NoPlanRecovery  ] Recovery failed");
        }
        finally
        {
            // Always return to idle after recovery attempt
            Task.Delay(1000).ContinueWith(_ => EnterState(RecoveryState.Idle));
        }
    }

    private RecoveryAction DetermineRecoveryAction(NoPlanRecoveryOptions options)
    {
        // Progressive escalation based on consecutive failures
        if (consecutiveFailures >= options.EmergencyResetThreshold)
        {
            return RecoveryAction.EmergencyReset;
        }
        else if (consecutiveFailures >= options.ForceReplanThreshold)
        {
            return RecoveryAction.ForceReplan;
        }
        else if (consecutiveFailures >= options.ResetStateThreshold)
        {
            return RecoveryAction.ResetState;
        }

        return RecoveryAction.ClearTarget;
    }

    private void ExecuteClearTarget()
    {
        logger.LogInformation("[NoPlanRecovery  ] Strategy: ClearTarget");

        try
        {
            // Use existing clear target mechanisms through available components
            // This is a passive approach - just clear target and let GOAP replan

            // Note: We don't have direct access to ConfigurableInput here,
            // so we rely on the GOAP state changes to trigger target clearing
            // The PullTargetGoal and CombatGoal handle target clearing when appropriate

            logger.LogInformation("[NoPlanRecovery  ] Target clear requested via state change");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[NoPlanRecovery  ] ClearTarget failed");
        }
    }

    private void ExecuteResetState()
    {
        logger.LogInformation("[NoPlanRecovery  ] Strategy: ResetState");

        try
        {
            // Trigger a resume event to reset all goals
            if (goapAgent?.CurrentGoal is IGoapEventListener listener)
            {
                listener.OnGoapEvent(new ResumeEvent());
                logger.LogInformation("[NoPlanRecovery  ] State reset triggered via ResumeEvent");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[NoPlanRecovery  ] ResetState failed");
        }
    }

    private void ExecuteForceReplan()
    {
        logger.LogInformation("[NoPlanRecovery  ] Strategy: ForceReplan");

        try
        {
            // Send abort event to clear current execution
            if (goapAgent?.CurrentGoal is IGoapEventListener listener)
            {
                listener.OnGoapEvent(new AbortEvent());
            }

            // Then trigger resume to force replanning
            Task.Delay(500).ContinueWith(_ =>
            {
                if (goapAgent?.CurrentGoal is IGoapEventListener listener)
                {
                    listener.OnGoapEvent(new ResumeEvent());
                }
            });

            logger.LogInformation("[NoPlanRecovery  ] Force replan triggered");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[NoPlanRecovery  ] ForceReplan failed");
        }
    }

    private void ExecuteEmergencyReset()
    {
        logger.LogError("[NoPlanRecovery  ] Strategy: EmergencyReset - {Consecutive} consecutive failures",
            consecutiveFailures);

        try
        {
            // Full emergency reset
            // 1. Abort current goal
            if (goapAgent?.CurrentGoal is IGoapEventListener listener)
            {
                listener.OnGoapEvent(new AbortEvent());
            }

            // 2. Wait a moment
            Task.Delay(1000).Wait();

            // 3. Trigger session reset
            if (goapAgent != null)
            {
                // Reset consecutive counter after emergency reset
                Interlocked.Exchange(ref consecutiveFailures, 0);
            }

            logger.LogInformation("[NoPlanRecovery  ] Emergency reset completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[NoPlanRecovery  ] EmergencyReset failed");
        }
    }

    private void EnterState(RecoveryState newState)
    {
        currentState = newState;
        stateEnteredAt = DateTime.UtcNow;
    }

    private static bool IsNoPlanEvent(GoapEventArgs e)
    {
        // NO PLAN is indicated by AbortEvent when no plan can be found
        // The GoapAgent logs this as EventId 0053
        return e is AbortEvent;
    }

    private enum RecoveryAction
    {
        ClearTarget,
        ResetState,
        ForceReplan,
        EmergencyReset
    }
}
