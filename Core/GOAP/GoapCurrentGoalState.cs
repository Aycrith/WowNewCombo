using Core.Goals;

using System;
using System.Threading;

namespace Core.GOAP;

public sealed class GoapCurrentGoalState
{
    private const string NoneGoalName = "None";

    private string currentGoalName = "None";
    private DateTime lastUpdatedUtc = DateTime.MinValue;
    private long goalStartTicks;
    private int transitionCount;

    public string CurrentGoalName => Volatile.Read(ref currentGoalName);

    public DateTime LastUpdatedUtc => lastUpdatedUtc;

    public TimeSpan CurrentGoalAge
    {
        get
        {
            if (string.Equals(CurrentGoalName, NoneGoalName, StringComparison.Ordinal))
            {
                return TimeSpan.Zero;
            }

            long startTicks = Volatile.Read(ref goalStartTicks);
            if (startTicks <= 0)
            {
                return TimeSpan.Zero;
            }

            long ageTicks = Math.Max(0, DateTime.UtcNow.Ticks - startTicks);
            return TimeSpan.FromTicks(ageTicks);
        }
    }

    public int TransitionCount => Volatile.Read(ref transitionCount);

    public bool IsCurrentGoal(string goalTypeName)
    {
        if (string.IsNullOrWhiteSpace(goalTypeName))
        {
            return false;
        }

        return string.Equals(CurrentGoalName, goalTypeName, StringComparison.Ordinal);
    }

    public void SetCurrentGoal(GoapGoal? goal)
    {
        SetCurrentGoalName(goal?.GetType().Name);
    }

    public void SetCurrentGoalName(string? goalTypeName)
    {
        string value = string.IsNullOrWhiteSpace(goalTypeName) ? NoneGoalName : goalTypeName;
        string previous = Volatile.Read(ref currentGoalName);
        bool changed = !string.Equals(previous, value, StringComparison.Ordinal);

        long nowTicks = DateTime.UtcNow.Ticks;
        Volatile.Write(ref currentGoalName, value);
        if (changed)
        {
            Volatile.Write(ref goalStartTicks, string.Equals(value, NoneGoalName, StringComparison.Ordinal) ? 0L : nowTicks);
            Interlocked.Increment(ref transitionCount);
        }

        lastUpdatedUtc = DateTime.UtcNow;
    }

    public void Clear()
    {
        SetCurrentGoalName(null);
    }
}
