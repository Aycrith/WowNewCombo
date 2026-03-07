using Core.Addon;
using Core.GoalsComponent;
using Core.GOAP;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SharedLib.Extensions;

using System;
using System.Diagnostics;
using System.Numerics;

namespace Core.Goals;

public sealed class AutoGatherGoal : GoapGoal, IGoapEventListener, IRouteProvider, IDisposable
{
    public const string KeyActionName = "AutoGathering";

    public override float Cost => key.Cost;
    public DateTime LastActive => navigation.LastActive;

    private readonly ILogger<AutoGatherGoal> logger;
    private readonly ConfigurableInput input;
    private readonly Navigation navigation;
    private readonly KeyAction key;
    private readonly PlayerReader playerReader;
    private readonly Wait wait;
    private readonly AddonBits bits;
    private readonly FoundNodeListener foundNodeListener;

    /// <summary>When the bot arrives at the estimated node coordinate and SoftInteract stays empty,
    /// this stopwatch tracks how long we have been searching. If it exceeds the search timeout we give up.</summary>
    private readonly Stopwatch searchStopwatch = new();
    private bool inSearchPhase;
    private const int SearchTimeoutMs = 4000;
    private const int SearchTurnStepMs = 400;

    public AutoGatherGoal(
        ILogger<AutoGatherGoal> logger,
        ConfigurableInput input,
        Navigation navigation,
        PlayerReader playerReader,
        Wait wait,
        AddonBits bits,
        FoundNodeListener foundNodeListener,
        [FromKeyedServices(KeyActionName)] KeyAction keyAction
        ) : base(nameof(AutoGatherGoal))
    {
        this.logger = logger;
        this.input = input;
        this.wait = wait;
        this.playerReader = playerReader;
        this.navigation = navigation;
        this.foundNodeListener = foundNodeListener;
        this.bits = bits;

        key = keyAction;

        foundNodeListener.NodeFound += FoundNode;
        navigation.OnNoPathFound += Navigation_OnStuck;
        navigation.OnDestinationReached += Navigation_OnDestinationReached;
    }

    public void Dispose()
    {
        foundNodeListener.NodeFound -= FoundNode;
        navigation.OnNoPathFound -= Navigation_OnStuck;
        navigation.OnDestinationReached -= Navigation_OnDestinationReached;

        navigation.Dispose();
    }

    public override bool CanRun() =>
        !IsCircuitBreakerOpen() &&
        key.Path != Array.Empty<Vector3>() &&
        (key.Path.Length == 1 && key.Path[0] != default) &&
        key.CanRun();

    public bool HasNext()
    {
        return navigation.HasNext();
    }

    public Vector3[] MapRoute()
    {
        return key.Path;
    }

    public Vector3 NextMapPoint()
    {
        return navigation.NextMapPoint();
    }
    public Vector3[] PathingRoute()
    {
        return navigation.TotalRoute;
    }

    public void OnGoapEvent(GoapEventArgs e)
    {
        if (e.GetType() == typeof(ResumeEvent))
        {
            Resume();

        }
        else if (e.GetType() == typeof(AbortEvent))
        {
            Abort();
        }
    }

    private void Resume()
    {
        navigation.Resume();
    }

    private void Abort()
    {
        navigation.StopMovement();
        navigation.Stop();
    }

    public override void OnEnter() => Resume();

    public override void OnExit() => Abort();

    public override void Update()
    {
        if (bits.Drowning())
            input.PressJump();

        // If SoftInteract is live and the object is a valid node, interact immediately.
        bool softInteractTriggered = false;
        if (bits.SoftInteract_Enabled())
        {
            int id = playerReader.SoftInteract_Id;

            if (id != 0 && (GameObject.IsMineral(id) || GameObject.IsHerb(id)))
            {
                input.PressInteract();
                wait.Update();
                softInteractTriggered = true;

                // Cancel the search phase if a valid node was found during it.
                if (inSearchPhase)
                {
                    inSearchPhase = false;
                    searchStopwatch.Reset();
                    logger.LogInformation("[AutoGatherGoal   ] Node found during search phase — interacting.");
                }
            }
        }

        // Search phase: bot has arrived at the destination vector but SoftInteract_Id is still 0.
        if (inSearchPhase && !softInteractTriggered)
        {
            if (searchStopwatch.ElapsedMilliseconds > SearchTimeoutMs)
            {
                logger.LogWarning("[AutoGatherGoal   ] Search timeout — no node found near destination. Giving up.");
                inSearchPhase = false;
                searchStopwatch.Reset();
                RegisterNodeFailure();
                key.Path = [];
            }
            else
            {
                // Periodically turn to sweep the camera frustum around the estimated node position.
                if (searchStopwatch.ElapsedMilliseconds % (SearchTurnStepMs * 2) < SearchTurnStepMs)
                    input.TurnRandomDir(SearchTurnStepMs);
            }

            wait.Update();
            return; // Don't run navigation.Update() while searching — bot is already at destination.
        }

        navigation.Update();

        wait.Update();
    }

    private void Navigation_OnDestinationReached()
    {
        // Navigation just reached the estimated node coordinate.
        // If SoftInteract has not been triggered yet, start the search phase.
        int id = playerReader.SoftInteract_Id;
        bool nodeInRange = bits.SoftInteract_Enabled() && id != 0 &&
                           (GameObject.IsMineral(id) || GameObject.IsHerb(id));

        if (!nodeInRange && key.Path.Length > 0)
        {
            logger.LogWarning("[AutoGatherGoal   ] Arrived at node coordinate but SoftInteract not ready — starting search.");
            inSearchPhase = true;
            searchStopwatch.Restart();
        }
    }

    public void FoundNode(Vector3 node)
    {
        if (node == default)
        {
            key.Path = [];
            return;
        }

        if (IsNodeBlacklisted(node))
        {
            return;
        }

        if (key.Path.Length == 1 && key.Path[0] == node)
        {
            return;
        }

        if (key.Path.Length > 0 && Vector2.Distance(node.AsVector2(), key.Path[0].AsVector2()) < 0.05f)
        {
            return;
        }

        logger.LogWarning($"Found node at {node}");

        key.Path = [node];
        navigation.SetWayPoints(key.Path);
    }

    private readonly System.Collections.Generic.List<(Vector3 pos, int attempts, DateTime time)> blacklistedNodes = [];
    private const int MaxFailedAttempts = 3;
    private const float BlacklistRadius = 5.0f;
    private DateTime lastStuckTime;

    // Pathfinder circuit breaker (Gap B fix)
    // Tracks how many distinct destinations fail in a short window.
    // If the window fills, gathering pauses to avoid mass-blacklisting every node
    // on the minimap when the pathing service (e.g. AmeisenNavigation) is offline.
    private readonly System.Collections.Generic.List<DateTime> recentPathFailureTimes = [];
    private const int CircuitBreakerThreshold = 5;     // failures within window
    private const double CircuitBreakerWindowSecs = 10; // seconds
    private const double CircuitBreakerPauseSecs = 30;  // pause duration when tripped
    private DateTime circuitBreakerOpenUntil = DateTime.MinValue;

    private bool IsCircuitBreakerOpen()
    {
        if (DateTime.UtcNow < circuitBreakerOpenUntil)
        {
            return true;
        }

        // Prune stale entries
        recentPathFailureTimes.RemoveAll(t => (DateTime.UtcNow - t).TotalSeconds > CircuitBreakerWindowSecs);
        return false;
    }

    private void RecordPathFailure()
    {
        recentPathFailureTimes.Add(DateTime.UtcNow);

        if (recentPathFailureTimes.Count >= CircuitBreakerThreshold)
        {
            circuitBreakerOpenUntil = DateTime.UtcNow.AddSeconds(CircuitBreakerPauseSecs);
            recentPathFailureTimes.Clear();
            logger.LogError($"[AutoGatherGoal   ] Pathfinder circuit breaker tripped: "
                          + $"{CircuitBreakerThreshold} path failures in {CircuitBreakerWindowSecs}s. "
                          + $"Gathering paused for {CircuitBreakerPauseSecs}s — verify AmeisenNavigation is running.");

            // Clear the current node so CanRun() returns false while paused.
            key.Path = [];
            navigation.StopMovement();
            navigation.Stop();
        }
    }

    private bool IsNodeBlacklisted(Vector3 node)
    {
        blacklistedNodes.RemoveAll(x => (DateTime.UtcNow - x.time).TotalMinutes > 10);
        foreach (var blocked in blacklistedNodes)
        {
            // Use full 3D distance so nodes on different elevations (e.g. cliff above vs. ground below)
            // are treated as distinct locations and don't shadow each other.
            if (Vector3.Distance(node, blocked.pos) < BlacklistRadius && blocked.attempts >= MaxFailedAttempts)
            {
                return true;
            }
        }
        return false;
    }

    private void RegisterNodeFailure()
    {
        if (key.Path.Length == 0) return;

        Vector3 currentNode = key.Path[0];
        
        for (int i = 0; i < blacklistedNodes.Count; i++)
        {
            if (Vector3.Distance(currentNode, blacklistedNodes[i].pos) < BlacklistRadius)
            {
                var entry = blacklistedNodes[i];
                entry.attempts++;
                entry.time = DateTime.UtcNow;
                blacklistedNodes[i] = entry;

                if (entry.attempts >= MaxFailedAttempts)
                {
                    logger.LogWarning($"Node at {currentNode} blacklisted due to repeated gathering failures.");
                    key.Path = []; // Clear current path to stop trying
                    navigation.StopMovement();
                    navigation.Stop();
                }
                return;
            }
        }

        blacklistedNodes.Add((currentNode, 1, DateTime.UtcNow));
    }

    private void Navigation_OnStuck()
    {
        if ((DateTime.UtcNow - lastStuckTime).TotalSeconds > 5)
        {
            lastStuckTime = DateTime.UtcNow;
            RecordPathFailure(); // Feed circuit breaker
            RegisterNodeFailure();
        }
    }
}
