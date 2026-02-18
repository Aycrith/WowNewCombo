using Core;
using Core.GOAP;

using Microsoft.Extensions.Logging;

using System.Threading;

namespace Core.Diagnostics;

/// <summary>
/// Diagnostic tool to troubleshoot binding and target clearing issues
/// </summary>
public class BindingDiagnostics
{
    private readonly ILogger<BindingDiagnostics> logger;
    private readonly ConfigurableInput input;
    private readonly AddonBits bits;
    private readonly PlayerReader playerReader;
    private readonly IBlacklist blacklist;
    private readonly ExecGameCommand execGameCommand;
    private readonly Wait wait;
    private readonly StuckDetector stuckDetector;
    private readonly CancellationToken token;

    public BindingDiagnostics(
        ILogger<BindingDiagnostics> logger,
        ConfigurableInput input,
        AddonBits bits,
        PlayerReader playerReader,
        IBlacklist blacklist,
        ExecGameCommand execGameCommand,
        Wait wait,
        StuckDetector stuckDetector,
        CancellationTokenSource cts)
    {
        this.logger = logger;
        this.input = input;
        this.bits = bits;
        this.playerReader = playerReader;
        this.blacklist = blacklist;
        this.execGameCommand = execGameCommand;
        this.wait = wait;
        this.stuckDetector = stuckDetector;
        this.token = cts.Token;
    }

    /// <summary>
    /// Runs comprehensive binding diagnostics
    /// </summary>
    public void RunDiagnostics()
    {
        logger.LogInformation("=== BINDING DIAGNOSTICS START ===");

        // Check 1: ClearTarget KeyAction configuration
        CheckClearTargetConfiguration();

        // Check 2: Game bindings received from addon
        CheckGameBindings();

        // Check 3: Current target state
        CheckTargetState();

        // Check 4: Test target clearing
        TestTargetClearing();

        // Check 5: Check blacklist status
        CheckBlacklistStatus();

        logger.LogInformation("=== BINDING DIAGNOSTICS END ===");
    }

    private void CheckClearTargetConfiguration()
    {
        logger.LogInformation("[DIAG] Checking ClearTarget configuration...");

        var clearTarget = input.ClearTarget;
        logger.LogInformation($"[DIAG] ClearTarget.Name: {clearTarget.Name}");
        logger.LogInformation($"[DIAG] ClearTarget.Key: {clearTarget.Key}");
        logger.LogInformation($"[DIAG] ClearTarget.ConsoleKey: {clearTarget.ConsoleKey}");
        logger.LogInformation($"[DIAG] ClearTarget.BindingID: {clearTarget.BindingID}");
        logger.LogInformation($"[DIAG] ClearTarget.HasModifier: {clearTarget.HasModifier}");
        logger.LogInformation($"[DIAG] ClearTarget.Modifier: {clearTarget.Modifier}");
        logger.LogInformation($"[DIAG] ClearTarget.Slot: {clearTarget.Slot}");

        if (clearTarget.ConsoleKey == System.ConsoleKey.NoName)
        {
            logger.LogError("[DIAG] ERROR: ClearTarget.ConsoleKey is NoName! Binding not resolved.");
        }
        else
        {
            logger.LogInformation($"[DIAG] OK: ClearTarget bound to {clearTarget.ConsoleKey}");
        }
    }

    private void CheckGameBindings()
    {
        logger.LogInformation("[DIAG] Checking game bindings from addon...");
        logger.LogInformation($"[DIAG] GameBindings count: {KeyReader.GameBindings.Count}");

        foreach (var binding in KeyReader.GameBindings)
        {
            logger.LogInformation($"[DIAG] GameBinding: {binding.Key} -> {binding.Value.Key} (mod: {binding.Value.Modifier})");
        }

        if (KeyReader.GameBindings.TryGetValue(BindingID.CUSTOM_CLEARTARGET, out var clearTargetBinding))
        {
            logger.LogInformation($"[DIAG] OK: CUSTOM_CLEARTARGET received from addon: {clearTargetBinding.Key}");
        }
        else
        {
            logger.LogWarning("[DIAG] WARNING: CUSTOM_CLEARTARGET not received from addon yet!");
        }

        if (KeyReader.GameBindings.TryGetValue(BindingID.CUSTOM_STOPATTACK, out var stopAttackBinding))
        {
            logger.LogInformation($"[DIAG] OK: CUSTOM_STOPATTACK received from addon: {stopAttackBinding.Key}");
        }
        else
        {
            logger.LogWarning("[DIAG] WARNING: CUSTOM_STOPATTACK not received from addon yet!");
        }
    }

    private void CheckTargetState()
    {
        logger.LogInformation("[DIAG] Checking current target state...");
        logger.LogInformation($"[DIAG] bits.Target(): {bits.Target()}");
        logger.LogInformation($"[DIAG] bits.Target_Dead(): {bits.Target_Dead()}");
        logger.LogInformation($"[DIAG] bits.Target_Hostile(): {bits.Target_Hostile()}");
        logger.LogInformation($"[DIAG] bits.Target_Tagged(): {bits.Target_Tagged()}");
        logger.LogInformation($"[DIAG] playerReader.TargetGuid: {playerReader.TargetGuid}");
        logger.LogInformation($"[DIAG] playerReader.TargetId: {playerReader.TargetId}");
        logger.LogInformation($"[DIAG] playerReader.TargetLevel: {playerReader.TargetLevel}");
    }

    private void TestTargetClearing()
    {
        if (!bits.Target())
        {
            logger.LogInformation("[DIAG] No target to clear, skipping test.");
            return;
        }

        logger.LogInformation("[DIAG] Testing target clearing...");
        logger.LogInformation($"[DIAG] Target before clear: {bits.Target()}");

        // Test 1: Try key binding
        logger.LogInformation("[DIAG] Attempting to clear target via key binding...");
        input.PressClearTarget();
        wait.Update();
        logger.LogInformation($"[DIAG] Target after key press: {bits.Target()}");

        if (bits.Target())
        {
            // Test 2: Try slash command
            logger.LogInformation("[DIAG] Key binding failed, trying slash command...");
            execGameCommand.Run("/cleartarget", logMessage: "[DIAG] Executing /cleartarget");
            wait.Update();
            logger.LogInformation($"[DIAG] Target after slash command: {bits.Target()}");
        }

        if (bits.Target())
        {
            logger.LogError("[DIAG] ERROR: Failed to clear target! Both key binding and slash command failed.");
        }
        else
        {
            logger.LogInformation("[DIAG] OK: Target cleared successfully!");
        }
    }

    private void CheckBlacklistStatus()
    {
        logger.LogInformation("[DIAG] Checking blacklist status...");
        logger.LogInformation($"[DIAG] blacklist.Is(): {blacklist.Is()}");

        if (blacklist is Blacklist<BlacklistTarget> typedBlacklist)
        {
            logger.LogInformation($"[DIAG] Blacklist type: {typedBlacklist.GetType().Name}");
        }
    }
}
