using System;

namespace Core.Testing;

/// <summary>
/// Snapshot of player state at a point in time for comparison
/// </summary>
public record PlayerStateSnapshot(
    float MapX,
    float MapY,
    float Direction,
    int UIMapId,
    int Level,
    int Health,
    int HealthMax,
    int HealthPercent,
    int PowerCurrent,
    int PowerMax,
    int ManaCurrent,
    int ManaMax,
    int ComboPoints,
    bool InCombat,
    bool HasTarget,
    bool TargetAlive,
    bool TargetHostile,
    bool Stealthed,
    bool Falling,
    bool Moving,
    bool Mounted,
    bool Dead,
    bool Swimming,
    int TargetHealth,
    int TargetHealthMax,
    int TargetId,
    int TargetLevel,
    string TargetClassification,
    DateTime CapturedAt)
{
    /// <summary>
    /// Create a snapshot from current player reader state
    /// </summary>
    public static PlayerStateSnapshot Capture(PlayerReader player, AddonBits bits)
    {
        return new PlayerStateSnapshot(
            MapX: player.MapX,
            MapY: player.MapY,
            Direction: player.Direction,
            UIMapId: player.UIMapId.Value,
            Level: player.Level.Value,
            Health: player.HealthCurrent(),
            HealthMax: player.HealthMax(),
            HealthPercent: player.HealthPercent(),
            PowerCurrent: player.PTCurrent(),
            PowerMax: player.PTMax(),
            ManaCurrent: player.ManaCurrent(),
            ManaMax: player.ManaMax(),
            ComboPoints: player.ComboPoints(),
            InCombat: bits.Combat(),
            HasTarget: bits.Target(),
            TargetAlive: bits.Target() && !bits.Target_Dead(),
            TargetHostile: bits.Target_Hostile(),
            Stealthed: bits.Stealthed(),
            Falling: bits.Falling(),
            Moving: bits.Moving(),
            Mounted: bits.Mounted(),
            Dead: bits.Dead(),
            Swimming: bits.Swimming(),
            TargetHealth: bits.Target() ? player.TargetHealth() : 0,
            TargetHealthMax: bits.Target() ? player.TargetMaxHealth() : 0,
            TargetId: bits.Target() ? player.TargetId : 0,
            TargetLevel: bits.Target() ? player.TargetLevel : 0,
            TargetClassification: bits.Target() ? player.TargetClassification.ToString() : "None",
            CapturedAt: DateTime.UtcNow);
    }

    /// <summary>
    /// Calculate distance moved from another snapshot
    /// </summary>
    public float DistanceFrom(PlayerStateSnapshot other)
    {
        float dx = MapX - other.MapX;
        float dy = MapY - other.MapY;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Calculate direction change from another snapshot (in radians)
    /// </summary>
    public float DirectionChangeFrom(PlayerStateSnapshot other)
    {
        return MathF.Abs(Direction - other.Direction);
    }

    /// <summary>
    /// Get time elapsed since another snapshot
    /// </summary>
    public TimeSpan TimeSince(PlayerStateSnapshot other)
    {
        return CapturedAt - other.CapturedAt;
    }
}
