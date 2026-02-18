namespace Core.Hazard;

/// <summary>
/// Types of hazard events that can trigger avoidance.
/// </summary>
public enum HazardEventType : byte
{
    /// <summary>Bot got stuck and required recovery.</summary>
    Stuck = 1,

    /// <summary>Player character died.</summary>
    Death = 2,

    /// <summary>Target evaded and reset.</summary>
    TargetEvade = 3,

    /// <summary>Pathfinding failed to find a route.</summary>
    PathfindingFailure = 4,

    /// <summary>Combat initiated by hostile NPC (unwanted pull).</summary>
    UnexpectedAggro = 5,

    /// <summary>Manual hazard marker placed by user.</summary>
    ManualMarker = 99
}

