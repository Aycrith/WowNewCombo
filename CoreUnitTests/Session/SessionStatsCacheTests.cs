using Core;
using Core.GOAP;
using Core.Goals;

using System;
using System.Collections.Specialized;
using System.Reflection;
using System.Runtime.CompilerServices;

using Xunit;

namespace CoreUnitTests.Session;

public sealed class SessionStatsCacheTests
{
    [Fact]
    public void Capture_FromSessionStat_StoresSnapshot()
    {
        SessionStatsCache cache = new();
        SessionStat stats = new()
        {
            Kills = 7,
            Deaths = 1,
            StartTime = System.Diagnostics.Stopwatch.GetTimestamp() - (long)(TimeSpan.FromMinutes(65).TotalSeconds * System.Diagnostics.Stopwatch.Frequency)
        };

        cache.Capture(stats, BotRuntimeModeHelper.Live, "CombatGoal", botActive: true);

        CachedSessionStats? snapshot = cache.GetSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal(7, snapshot!.Kills);
        Assert.Equal(1, snapshot.Deaths);
        Assert.Equal("CombatGoal", snapshot.CurrentGoal);
        Assert.True(snapshot.BotActive);
        Assert.Equal("live", snapshot.RuntimeMode);
    }

    [Fact]
    public void Capture_FromGoapAgent_UsesCurrentGoalTypeName()
    {
        SessionStatsCache cache = new();
        SessionStat stats = new()
        {
            Kills = 4,
            Deaths = 0,
            StartTime = System.Diagnostics.Stopwatch.GetTimestamp() - (long)(TimeSpan.FromMinutes(10).TotalSeconds * System.Diagnostics.Stopwatch.Frequency)
        };

        GoapAgent agent = CreateAgent(stats, active: false, currentGoal: new CacheTestGoal());

        cache.Capture(agent, BotRuntimeModeHelper.Live);

        CachedSessionStats? snapshot = cache.GetSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal(nameof(CacheTestGoal), snapshot!.CurrentGoal);
        Assert.False(snapshot.BotActive);
    }

    [Fact]
    public void Capture_FromInactiveGoapAgent_PreservesFinalCounters()
    {
        SessionStatsCache cache = new();
        SessionStat stats = new()
        {
            Kills = 11,
            Deaths = 2,
            StartTime = System.Diagnostics.Stopwatch.GetTimestamp() - (long)(TimeSpan.FromMinutes(55).TotalSeconds * System.Diagnostics.Stopwatch.Frequency)
        };

        GoapAgent agent = CreateAgent(stats, active: false, currentGoal: new CacheTestGoal());

        cache.Capture(agent, BotRuntimeModeHelper.Live);

        CachedSessionStats? snapshot = cache.GetSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal(11, snapshot!.Kills);
        Assert.Equal(2, snapshot.Deaths);
        Assert.False(snapshot.BotActive);
        Assert.Equal(BotRuntimeModeHelper.Live, snapshot.RuntimeMode);
    }

    private static GoapAgent CreateAgent(SessionStat sessionStat, bool active, GoapGoal currentGoal)
    {
        GoapAgent agent = (GoapAgent)RuntimeHelpers.GetUninitializedObject(typeof(GoapAgent));
        SetField(agent, "active", active);
        SetAutoProperty(agent, "SessionStat", sessionStat);
        SetAutoProperty(agent, "CurrentGoal", currentGoal);
        SetAutoProperty(agent, "AvailableGoals", new GoapGoal[] { currentGoal });
        SetAutoProperty(agent, "Plan", new System.Collections.Generic.Stack<GoapGoal>());
        SetAutoProperty(agent, "WorldState", new BitVector32());
        return agent;
    }

    private static void SetAutoProperty(object target, string propertyName, object? value)
    {
        FieldInfo field = target.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Backing field for property '{propertyName}' was not found.");
        field.SetValue(target, value);
    }

    private static void SetField(object target, string fieldName, object? value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");
        field.SetValue(target, value);
    }

    private sealed class CacheTestGoal() : GoapGoal(nameof(CacheTestGoal))
    {
        public override float Cost => 1f;
    }
}
