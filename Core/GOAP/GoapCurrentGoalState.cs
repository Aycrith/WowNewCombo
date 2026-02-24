using Core.Goals;

using System;
using System.Threading;

namespace Core.GOAP;

public sealed class GoapCurrentGoalState
{
    private string currentGoalName = "None";
    private DateTime lastUpdatedUtc = DateTime.MinValue;

    public string CurrentGoalName => Volatile.Read(ref currentGoalName);

    public DateTime LastUpdatedUtc => lastUpdatedUtc;

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
        string value = string.IsNullOrWhiteSpace(goalTypeName) ? "None" : goalTypeName;
        Volatile.Write(ref currentGoalName, value);
        lastUpdatedUtc = DateTime.UtcNow;
    }

    public void Clear()
    {
        SetCurrentGoalName(null);
    }
}
