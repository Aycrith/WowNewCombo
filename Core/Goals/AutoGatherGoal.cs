using Core.Addon;
using Core.GoalsComponent;
using Core.GOAP;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SharedLib.Extensions;

using System;
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
    }

    public void Dispose()
    {
        foundNodeListener.NodeFound -= FoundNode;
        navigation.OnNoPathFound -= Navigation_OnStuck;

        navigation.Dispose();
    }

    public override bool CanRun() =>
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

        if (bits.SoftInteract_Enabled())
        {
            int id = playerReader.SoftInteract_Id;

            if (id != 0 && (GameObject.IsMineral(id) || GameObject.IsHerb(id)))
            {
                input.PressInteract();
                wait.Update();
            }
        }

        //if (pathState != PathState.Finished)
        navigation.Update();

        wait.Update();
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

    private bool IsNodeBlacklisted(Vector3 node)
    {
        blacklistedNodes.RemoveAll(x => (DateTime.UtcNow - x.time).TotalMinutes > 10);
        foreach (var blocked in blacklistedNodes)
        {
            if (Vector2.Distance(node.AsVector2(), blocked.pos.AsVector2()) < BlacklistRadius && blocked.attempts >= MaxFailedAttempts)
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
            if (Vector2.Distance(currentNode.AsVector2(), blacklistedNodes[i].pos.AsVector2()) < BlacklistRadius)
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
            RegisterNodeFailure();
        }
    }
}
