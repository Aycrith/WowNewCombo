using Core.GOAP;

using Microsoft.Extensions.Logging;

using System;
using System.Buffers;
using System.Numerics;

namespace Core.Goals;

public sealed class FleeGoal : GoapGoal, IRouteProvider
{
    public override float Cost => 3.1f;

    private readonly ILogger<CombatGoal> logger;
    private readonly ConfigurableInput input;
    private readonly ClassConfiguration classConfig;
    private readonly Wait wait;
    private readonly PlayerReader playerReader;
    private readonly Navigation navigation;
    private readonly AddonBits bits;
    private readonly CombatLog combatLog;
    private readonly GoapAgentState goapAgentState;

    private readonly SafeSpotCollector safeSpotCollector;

    private Vector3[] MapPoints = [];

    public int MOB_COUNT = 1;

    public FleeGoal(ILogger<CombatGoal> logger, ConfigurableInput input,
        Wait wait, PlayerReader playerReader, AddonBits bits,
        ClassConfiguration classConfiguration, Navigation playerNavigation, GoapAgentState state,
        ClassConfiguration classConfig, CombatLog combatLog,
        SafeSpotCollector safeSpotCollector)
        : base(nameof(FleeGoal))
    {
        this.logger = logger;
        this.input = input;

        this.wait = wait;
        this.playerReader = playerReader;
        this.navigation = playerNavigation;
        this.bits = bits;
        this.combatLog = combatLog;

        this.classConfig = classConfig;
        this.goapAgentState = state;

        AddPrecondition(GoapKey.incombat, true);

        Keys = classConfiguration.Combat.Sequence;

        // this will ensure that the component is created
        this.safeSpotCollector = safeSpotCollector;
    }

    #region IRouteProvider

    public DateTime LastActive => navigation.LastActive;

    public Vector3[] MapRoute() => MapPoints;

    public Vector3[] PathingRoute()
    {
        return navigation.TotalRoute;
    }

    public bool HasNext()
    {
        return navigation.HasNext();
    }

    public Vector3 NextMapPoint()
    {
        return navigation.NextMapPoint();
    }

    #endregion

    public override bool CanRun()
    {
        return
            goapAgentState.SafeLocations.Count > 0 &&
            combatLog.DamageTakenCount() > MOB_COUNT;
    }

    public override void OnEnter()
    {
        // TODO: might have to do some pre processing like
        // straightening the path
        var count = goapAgentState.SafeLocations.Count;
        MapPoints = new Vector3[count];

        goapAgentState.SafeLocations.CopyTo(MapPoints, 0);

        navigation.SetWayPoints(MapPoints.AsSpan(0, count));
        navigation.ResetStuckParameters();
    }

    public override void OnExit()
    {
        goapAgentState.SafeLocations.Clear();

        navigation.Stop();
        navigation.StopMovement();
    }

    public override void Update()
    {
        if (bits.Target())
        {
            input.PressClearTarget();
        }

        wait.Update();
        navigation.Update();
    }
}
