using SixLabors.ImageSharp;

using System;

namespace SharedLib.Humanization;

public interface IHumanizationProvider
{
    bool Enabled { get; }

    double FatigueMultiplier { get; }

    bool IsOnBreak { get; }

    TimeSpan RemainingBreakTime { get; }

    int GetKeyHoldDurationMs(int baseMs);

    int GetInterKeyDelayMs(int baseMs);

    int GetPreActionReactionDelayMs(int complexity, bool isMovementAction);

    int BuildMousePath(Point start, Point end, Span<Point> buffer);
}

