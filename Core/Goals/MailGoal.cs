using Core.Database;
using Core.GOAP;

using Game;

using Microsoft.Extensions.Logging;

using SharedLib;
using SharedLib.Extensions;

using SixLabors.ImageSharp;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading;

namespace Core.Goals;

public sealed partial class MailGoal : GoapGoal, IGoapEventListener, IRouteProvider, IDisposable
{
    private enum MailState
    {
        ApproachMailbox,
        InteractMailbox,
        WaitForMailboxOpen,
        SendMail,
        WaitForMailFinished,
        Finished,
    }

    // Minimum mail fee: 30 copper for one mail (base cost even for gold-only)
    public const int MIN_MAIL_FEE = 30;

    private const int TIMEOUT = 15000;  // 15s to handle many mail batches with rate limiting delays
    private const int MAILBOX_INTERACT_TIMEOUT = 5000;
    private const int MAILBOX_INTERACT_ATTEMPTS = 6;
    private const int MAILBOX_INTERACT_STEP_TIMEOUT = 700;
    private const int MAX_MAILBOX_OPEN_FAILURES = 3;
    private const float MAILBOX_PROXIMITY_TRIGGER_DISTANCE = 2.5f;
    private const float MAILBOX_INTERACT_DISTANCE = 1.75f;

    public override float Cost => key.Cost;

    private readonly ILogger<MailGoal> logger;
    private readonly ConfigurableInput input;
    private readonly KeyAction key;
    private readonly Wait wait;
    private readonly Navigation navigation;
    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;
    private readonly StopMoving stopMoving;
    private readonly ClassConfiguration classConfig;
    private readonly AddonConfigurator addonConfigurator;
    private readonly IMountHandler mountHandler;
    private readonly CancellationToken token;
    private readonly ExecGameCommand execGameCommand;
    private readonly GossipReader gossipReader;
    private readonly BagReader bagReader;
    private readonly MailboxDB mailboxDB;
    private readonly CursorScan cursorScan;
    private readonly IMouseInput mouseInput;
    private readonly SessionStat sessionStat;

    private MailState mailState = MailState.Finished;
    private MailGossipState lastMailGossipState = MailGossipState.None;
    private int mailboxOpenFailures;
    private int mailboxCursorScanFailures;

    private readonly bool tryFindClosestMailbox;
    private Vector3 mailboxLocation;

    #region IRouteProvider

    public Vector3[] MapRoute() => [];

    public Vector3[] PathingRoute() => navigation.TotalRoute;

    public bool HasNext() => navigation.HasNext();

    public Vector3 NextMapPoint() => navigation.NextMapPoint();

    public DateTime LastActive => navigation.LastActive;

    #endregion

    public MailGoal(KeyAction key, ILogger<MailGoal> logger, ConfigurableInput input,
        Wait wait, PlayerReader playerReader, GossipReader gossipReader, AddonBits bits,
        Navigation navigation, StopMoving stopMoving, ClassConfiguration classConfig,
        AddonConfigurator addonConfigurator, BagReader bagReader, MailboxDB mailboxDB,
        CursorScan cursorScan, IMouseInput mouseInput, SessionStat sessionStat,
        IMountHandler mountHandler, ExecGameCommand exec, CancellationTokenSource cts)
        : base(nameof(MailGoal))
    {
        this.logger = logger;
        this.input = input;
        this.key = key;
        this.wait = wait;
        this.playerReader = playerReader;
        this.bits = bits;
        this.stopMoving = stopMoving;
        this.classConfig = classConfig;
        this.addonConfigurator = addonConfigurator;
        this.bagReader = bagReader;
        this.mailboxDB = mailboxDB;
        this.cursorScan = cursorScan;
        this.mouseInput = mouseInput;
        this.sessionStat = sessionStat;
        this.mountHandler = mountHandler;
        this.token = cts.Token;
        this.execGameCommand = exec;
        this.gossipReader = gossipReader;

        this.navigation = navigation;
        navigation.OnDestinationReached += Navigation_OnDestinationReached;
        navigation.OnWayPointReached += Navigation_OnWayPointReached;
        navigation.OnNoPathFound += Navigation_OnNoPathFound;

        // Mail should not happen in combat
        AddPrecondition(GoapKey.dangercombat, false);

        Keys = [key];

        tryFindClosestMailbox = key.Path.Length == 0;
    }

    public void Dispose()
    {
        navigation.Dispose();
    }

    public override bool CanRun() => key.CanRun();

    public void OnGoapEvent(GoapEventArgs e)
    {
        if (e is ResumeEvent)
        {
            Resume();
        }
        else if (e is AbortEvent)
        {
            Abort();
        }
    }

    private void Resume()
    {
        if (tryFindClosestMailbox && !TryAutoSelectMailbox())
        {
            mailState = MailState.Finished;
            LogWarn("No mailbox found nearby!");
            return;
        }

        input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
        stopMoving.Stop();

        SetMailboxDestination();

        navigation.Resume();

        mailState = MailState.ApproachMailbox;
        mailboxOpenFailures = 0;
        mailboxCursorScanFailures = 0;

        MountIfPossible();
    }

    private void Abort()
    {
        navigation.StopMovement();
        navigation.Stop();

        if (tryFindClosestMailbox)
        {
            key.Path = [];
            mailboxLocation = Vector3.Zero;
        }
    }

    public override void OnEnter() => Resume();

    public override void OnExit() => Abort();

    public override void Update()
    {
        if (bits.Drowning())
            input.PressJump();

        if (mailState != MailState.Finished)
        {
            CheckMailStateTransition();
            TryTriggerMailboxArrivalByProximity();
            navigation.Update();
        }

        wait.Update();
    }

    private void CheckMailStateTransition()
    {
        MailGossipState current = gossipReader.GetMailState();
        if (current != lastMailGossipState)
        {
            LogMailStateTransition(logger, lastMailGossipState.ToStringF(), current.ToStringF());
            lastMailGossipState = current;
        }
    }

    private void SetMailboxDestination()
    {
        if (key.Path.Length > 0)
        {
            mailboxLocation = key.Path[^1];
            navigation.SetWayPoints(key.Path.AsSpan());
        }
        else if (mailboxLocation != Vector3.Zero)
        {
            navigation.SetWayPoints([mailboxLocation]);
        }
    }

    private bool TryAutoSelectMailbox()
    {
        Vector3? nearest = mailboxDB.GetNearestMailbox(playerReader.UIMapId.Value, playerReader.WorldPos);
        if (nearest == null)
        {
            return false;
        }

        mailboxLocation = nearest.Value;
        key.Path = [mailboxLocation];

        LogFoundMailbox(logger, mailboxLocation);
        return true;
    }

    private void Navigation_OnNoPathFound()
    {
        if (mailState != MailState.ApproachMailbox || token.IsCancellationRequested)
            return;

        logger.LogError("No path to mailbox found!");
        Resume();
    }

    private void Navigation_OnWayPointReached()
    {
        if (mailState == MailState.ApproachMailbox)
        {
            LogDebug("Approaching mailbox...");
            navigation.SimplifyRouteToWaypoint = false;
        }
    }

    private void Navigation_OnDestinationReached()
    {
        if (mailState != MailState.ApproachMailbox || token.IsCancellationRequested)
            return;

        LogDebug("Reached mailbox location");
        navigation.StopMovement();
        stopMoving.Stop();
        wait.Update();

        mailState = MailState.InteractMailbox;

        float arrivalDistance = playerReader.WorldPos.WorldDistanceXYTo(mailboxLocation);
        if (arrivalDistance > MAILBOX_INTERACT_DISTANCE)
        {
            LogWarn($"Mailbox waypoint reached at distance {arrivalDistance:F2}; forcing closer approach.");
            ForceCloseApproachToMailbox();
        }

        if (!bits.MailFrameShown() && !InteractWithMailbox(allowMouseScan: true))
        {
            LogWarn("Failed to interact with mailbox");
            mailState = MailState.Finished;
            return;
        }

        mailState = MailState.WaitForMailboxOpen;

        float elapsed = -1;
        for (int cycle = 1; cycle <= MAX_MAILBOX_OPEN_FAILURES; cycle++)
        {
            elapsed = TryOpenMailboxWithRetries();
            if (elapsed >= 0)
            {
                mailboxOpenFailures = 0;
                break;
            }

            mailboxOpenFailures = cycle;
            if (cycle < MAX_MAILBOX_OPEN_FAILURES)
            {
                LogWarn($"Mailbox did not open (attempt cycle {cycle}/{MAX_MAILBOX_OPEN_FAILURES}); reattempting in place.");
                input.TurnRandomDir(150);
                wait.Update();
                if (!input.Approach.OnCooldown())
                {
                    input.PressApproach();
                    wait.Update();
                }
            }
        }

        if (elapsed < 0)
        {
            LogWarn($"Mailbox did not open after retries ({MAX_MAILBOX_OPEN_FAILURES} cycles).");
            mailState = MailState.Finished;
            return;
        }

        Log($"Mailbox opened after {elapsed}ms");
        mailState = MailState.SendMail;

        if (!StartMailSending())
        {
            CloseMailbox();
            return;
        }

        mailState = MailState.WaitForMailFinished;

        MailGossipState startState = gossipReader.GetMailState();

        // Wait for MAIL_SENDING or MAIL_FINISHED to confirm the addon has started/completed
        // MAIL_FINISHED can arrive directly when only one mail is sent (fast path)
        elapsed = wait.Until(TIMEOUT, () =>
        {
            MailGossipState state = gossipReader.GetMailState();
            if (state == MailGossipState.None)
            {
                return false;
            }

            if (state == startState)
            {
                return false;
            }

            return state == MailGossipState.Sending ||
                state == MailGossipState.Finished ||
                state == MailGossipState.SendFailed;
        });
        CheckMailStateTransition();
        if (elapsed < 0)
        {
            LogWarn($"Mail sending did not start within {TIMEOUT}ms");
            CloseMailbox();
            return;
        }

        // If already finished (fast path - single mail sent), skip second wait
        if (gossipReader.MailFinished())
        {
            Log($"Mail operation completed (fast path) after {elapsed}ms");
            CloseMailbox(success: true);
            return;
        }

        Log($"Mail sending started after {elapsed}ms");

        // Now wait for mail to finish
        elapsed = wait.Until(TIMEOUT, MailFinishedOrFailed);
        CheckMailStateTransition();
        if (elapsed < 0)
        {
            LogWarn($"Mail sending timeout after {TIMEOUT}ms");
        }
        else if (gossipReader.MailSendFailed())
        {
            LogWarn("Mail sending failed!");
        }
        else if (bits.NotMailFrameShown())
        {
            LogWarn("Mailbox closed unexpectedly during sending!");
        }
        else
        {
            Log($"Mail sending finished after {elapsed}ms");
            CloseMailbox(success: true);
            return;
        }

        CloseMailbox();
    }

    private void CloseMailbox(bool success = false)
    {
        // Deselect input box and Close MailboxFrame
        input.PressRandom(ConsoleKey.Escape, InputDuration.DefaultPress);
        input.PressRandom(ConsoleKey.Escape, InputDuration.DefaultPress);
        wait.Update();

        mailState = MailState.Finished;

        // Clear the vendor flag only on success, allowing retry on failure
        if (success)
        {
            sessionStat.VendoredOrRepairedRecently = false;
        }
    }

    private bool InteractWithMailbox(bool allowMouseScan)
    {
        // Try SoftInteract first (Cata+ clients)
        if (bits.SoftInteract_Enabled() && bits.SoftInteract())
        {
            // Check if soft target is a mailbox (GameObject type)
            if (playerReader.SoftInteract_Type == GuidType.GameObject)
            {
                Log("Interacting with mailbox via SoftInteract");
                input.PressInteract();
                return true;
            }
        }

        // Direct interact first to avoid unnecessary mouse control.
        input.PressInteract();
        wait.Update();

        if (bits.MailFrameShown() || !allowMouseScan)
        {
            return true;
        }

        // Mailbox-only mouse scan fallback (works even in keyboard-only mode).
        if (cursorScan.Find(CursorType.Mail, out Point mailboxCursorPoint))
        {
            // First try the configured interact-mouseover binding.
            LogDebug("Mailbox cursor found via scan; trying InteractMouseOver binding.");
            mouseInput.InteractMouseOver(token);
            wait.Update();

            if (bits.MailFrameShown())
            {
                LogDebug("Mailbox opened via InteractMouseOver.");
                return true;
            }

            // Some clients/configurations do not honor interact-mouseover reliably.
            // Fall back to direct click interaction on the detected mailbox cursor point.
            LogDebug("Mailbox not opened via InteractMouseOver; trying right-click fallback.");
            mouseInput.RightClick(mailboxCursorPoint);
            wait.Update();

            if (bits.MailFrameShown())
            {
                LogDebug("Mailbox opened via right-click fallback.");
                return true;
            }

            LogDebug("Mailbox still closed after right-click; trying left-click fallback.");
            mouseInput.LeftClick(mailboxCursorPoint);
            wait.Update();
            return true;
        }

        mailboxCursorScanFailures++;
        LogWarn("Mailbox cursor not found via scan");
        return true;
    }

    private float TryOpenMailboxWithRetries()
    {
        if (bits.MailFrameShown())
        {
            return 0;
        }

        float totalElapsed = 0;
        for (int attempt = 1; attempt <= MAILBOX_INTERACT_ATTEMPTS; attempt++)
        {
            bool allowMouseScan = attempt % 3 == 0 || attempt == MAILBOX_INTERACT_ATTEMPTS;
            InteractWithMailbox(allowMouseScan);

            float elapsed = wait.Until(MAILBOX_INTERACT_STEP_TIMEOUT, bits.MailFrameShown);
            CheckMailStateTransition();
            if (elapsed >= 0)
            {
                return totalElapsed + elapsed;
            }

            totalElapsed += MAILBOX_INTERACT_STEP_TIMEOUT;

            // Nudge orientation/position slightly between attempts.
            input.TurnRandomDir(120);
            wait.Update();
            if (!input.Approach.OnCooldown())
            {
                input.PressApproach();
                wait.Update();
            }

            if (mailboxCursorScanFailures > 0 && mailboxCursorScanFailures % 3 == 0)
            {
                ForceCloseApproachToMailbox();
            }
        }

        return -1;
    }

    private bool StartMailSending()
    {
        MailConfiguration mail = classConfig.GetEffectiveMailConfig();
        string addonName = addonConfigurator.Config.Title;

        string recipient = classConfig.GetEffectiveRecipientName();
        if (string.IsNullOrWhiteSpace(recipient))
        {
            LogWarn("No mail recipient configured! Set MAIL_RECIPIENT env var or RecipientName in config.");
            return false;
        }

        // Calculate mail fee: 30 copper per attachment slot
        // This ensures addon keeps enough gold to pay for mail fees
        IReadOnlySet<int> excludedSet = bagReader.BuildEffectiveMailExcludedItemIdSet(
            classConfig.GetEffectiveExcludedItemIdSet(),
            mail.ExcludeFoodAndDrink,
            mail.ExcludeConjuredItems);
        int itemCount = bagReader.CountMailableItems(mail.MinimumItemQuality, excludedSet);

        // Fee: 30 copper per item, or 30 copper base if gold-only
        int mailFee = itemCount > 0
            ? itemCount * MIN_MAIL_FEE
            : (mail.SendGold ? MIN_MAIL_FEE : 0);

        long adjustedGoldToKeep = mail.MinimumGoldToKeep + mailFee;

        // 1. Set mail config (fixed-size params)
        int sendGoldFlag = mail.SendGold ? 1 : 0;
        string configCmd = $"/run {addonName}:SMC(\"{recipient}\",{adjustedGoldToKeep},{mail.MinimumItemQuality},{sendGoldFlag})";
        string configLog = configCmd.Replace(recipient, "****");  // Hide recipient in logs
        execGameCommand.Run(configCmd, configLog);
        wait.Update();

        // 2. Send excluded item IDs in batches (if any)
        // Use effective exclusions which merge JSON config with runtime UI exclusions
        int[] effectiveExclusions = bagReader.BuildEffectiveMailExcludedItemIds(
            classConfig.GetEffectiveExcludedItemIds(),
            mail.ExcludeFoodAndDrink,
            mail.ExcludeConjuredItems);
        if (effectiveExclusions.Length > 0)
        {
            SendExcludedItemsBatched(addonName, effectiveExclusions);
        }

        // 3. Start mail sending
        string startCmd = $"/run {addonName}:SMS()";
        Log($"Starting mail sending to recipient (keeping at least {adjustedGoldToKeep} copper for fees)");
        execGameCommand.Run(startCmd);
        wait.Update();
        return true;
    }

    private void SendExcludedItemsBatched(string addonName, int[] excludedIds)
    {
        const int MAX_CMD_LENGTH = 250;  // Leave margin for safety (WoW limit is 255)
        string prefix = $"/run {addonName}:AEI(\"";
        string suffix = "\")";
        int overhead = prefix.Length + suffix.Length;

        StringBuilder batch = new();
        foreach (int id in excludedIds)
        {
            string idStr = id.ToString();
            // Check if adding this ID would exceed limit
            if (batch.Length > 0 && batch.Length + 1 + idStr.Length + overhead > MAX_CMD_LENGTH)
            {
                // Send current batch
                execGameCommand.Run($"{prefix}{batch}{suffix}");
                wait.Update();
                batch.Clear();
            }

            if (batch.Length > 0) batch.Append(',');
            batch.Append(idStr);
        }

        // Send remaining batch
        if (batch.Length > 0)
        {
            execGameCommand.Run($"{prefix}{batch}{suffix}");
            wait.Update();
        }
    }

    private bool MailFinishedOrFailed()
    {
        return gossipReader.MailFinished() || gossipReader.MailSendFailed() || bits.NotMailFrameShown();
    }

    private void MountIfPossible()
    {
        float totalDistance = VectorExt.TotalDistance<Vector3>(navigation.TotalRoute, VectorExt.WorldDistanceXY);

        // Check if key override allows mounting
        if (key.UseMount)
        {
            if (mountHandler.CanMount() && MountHandler.ShouldMount(totalDistance))
            {
                Log("Mounting for mailbox trip");
                mountHandler.MountUp();
                navigation.ResetStuckParameters();
                return;
            }
        }
        else
        {
            // Standard travel optimization: mount if possible, otherwise unstealth for speed
            mountHandler.OptimizeTravelSpeed(totalDistance);

            if (mountHandler.IsMounted())
            {
                navigation.ResetStuckParameters();
            }
        }
    }

    private void Log(string text) => logger.LogInformation(text);

    private void LogDebug(string text) => logger.LogDebug(text);

    private void LogWarn(string text) => logger.LogWarning(text);

    private void TryTriggerMailboxArrivalByProximity()
    {
        if (mailState != MailState.ApproachMailbox || mailboxLocation == Vector3.Zero)
        {
            return;
        }

        float distance = playerReader.WorldPos.WorldDistanceXYTo(mailboxLocation);
        if (distance > MAILBOX_PROXIMITY_TRIGGER_DISTANCE)
        {
            return;
        }

        LogDebug($"Mailbox proximity reached ({distance:F2}). Triggering interaction workflow.");
        Navigation_OnDestinationReached();
    }

    private void ForceCloseApproachToMailbox()
    {
        if (mailboxLocation == Vector3.Zero)
        {
            return;
        }

        if (mountHandler.IsMounted())
        {
            mountHandler.Dismount();
            wait.Update();
        }

        navigation.SetWayPoints([mailboxLocation]);
        navigation.Resume();

        wait.Until(MAILBOX_INTERACT_TIMEOUT, () =>
        {
            navigation.Update();
            wait.Update();
            float distance = playerReader.WorldPos.WorldDistanceXYTo(mailboxLocation);
            return distance <= MAILBOX_INTERACT_DISTANCE;
        });

        navigation.StopMovement();
        stopMoving.Stop();
        wait.Update();
    }

    #region Logging

    [LoggerMessage(
        EventId = 0400,
        Level = LogLevel.Information,
        Message = "Found nearest mailbox at {pos}")]
    static partial void LogFoundMailbox(ILogger logger, Vector3 pos);

    [LoggerMessage(
        EventId = 0401,
        Level = LogLevel.Information,
        Message = "[Gossip] {PreviousState} -> {CurrentState}")]
    static partial void LogMailStateTransition(ILogger logger, string previousState, string currentState);

    #endregion
}
