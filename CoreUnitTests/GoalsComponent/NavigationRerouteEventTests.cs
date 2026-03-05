using Core.Goals;
using NavigationGoal = Core.Goals.Navigation;

using FluentAssertions;

using System.Reflection;
using System.Runtime.CompilerServices;

using Xunit;

namespace CoreUnitTests.GoalsComponent;

public sealed class NavigationRerouteEventTests
{
    private static NavigationGoal CreateNavigationStub()
    {
        return (NavigationGoal)RuntimeHelpers.GetUninitializedObject(typeof(NavigationGoal));
    }

    private static void InvokePrivateMethod(NavigationGoal navigation, string methodName, params object?[] args)
    {
        MethodInfo? method = typeof(NavigationGoal).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull($"{methodName} should exist on Navigation");
        method!.Invoke(navigation, args);
    }

    [Fact]
    public void RecordRerouteTriggered_IncrementsCounterAndRaisesEvent()
    {
        NavigationGoal navigation = CreateNavigationStub();
        bool wasRaised = false;
        navigation.OnRerouteTriggered += () => wasRaised = true;

        InvokePrivateMethod(navigation, "RecordRerouteTriggered", 12.5f);

        Core.NavigationRerouteRuntimeSnapshot snapshot = navigation.GetRerouteRuntimeSnapshot();
        snapshot.RerouteTriggerCount.Should().Be(1);
        snapshot.LastRerouteAnchorDistance.Should().Be(12.5f);
        wasRaised.Should().BeTrue();
    }

    [Fact]
    public void RecordRerouteApplied_IncrementsCounterAndRaisesEvent()
    {
        NavigationGoal navigation = CreateNavigationStub();
        bool wasRaised = false;
        navigation.OnRerouteApplied += () => wasRaised = true;

        InvokePrivateMethod(navigation, "RecordRerouteApplied");

        Core.NavigationRerouteRuntimeSnapshot snapshot = navigation.GetRerouteRuntimeSnapshot();
        snapshot.RerouteApplyCount.Should().Be(1);
        wasRaised.Should().BeTrue();
    }

    [Fact]
    public void RecordRerouteDropped_IncrementsCounterAndRaisesEventWithReason()
    {
        NavigationGoal navigation = CreateNavigationStub();
        string? reason = null;
        navigation.OnRerouteDropped += value => reason = value;

        InvokePrivateMethod(navigation, "RecordRerouteDropped", "stale-or-target-mismatch", (float?)7.25f);

        Core.NavigationRerouteRuntimeSnapshot snapshot = navigation.GetRerouteRuntimeSnapshot();
        snapshot.RerouteDropCount.Should().Be(1);
        snapshot.LastRerouteDropReason.Should().Be("stale-or-target-mismatch");
        snapshot.LastRerouteAnchorDistance.Should().Be(7.25f);
        reason.Should().Be("stale-or-target-mismatch");
    }

    [Fact]
    public void RecordDetourOnlyCollapse_IncrementsCounterAndRaisesEvent()
    {
        NavigationGoal navigation = CreateNavigationStub();
        bool wasRaised = false;
        navigation.OnDetourOnlyCollapseDetected += () => wasRaised = true;

        InvokePrivateMethod(navigation, "RecordDetourOnlyCollapse");

        Core.NavigationRerouteRuntimeSnapshot snapshot = navigation.GetRerouteRuntimeSnapshot();
        snapshot.DetourOnlyCollapseCount.Should().Be(1);
        wasRaised.Should().BeTrue();
    }
}
