using Core;
using Core.Goals;

using FluentAssertions;

using System;

using Xunit;

namespace CoreUnitTests.GoalsComponent;

public class NpcNameTargetingTests
{
    [Fact]
    public void AcceptsCursor_ReturnsTrue_WhenCursorIsExplicitlyAllowed()
    {
        ReadOnlySpan<CursorType> allowed = [CursorType.Loot, CursorType.Vendor];

        NpcNameTargeting.AcceptsCursor(allowed, CursorType.Loot).Should().BeTrue();
    }

    [Fact]
    public void AcceptsCursor_ReturnsFalse_ForSkinWhenOnlyLootAndVendorAreAllowed()
    {
        ReadOnlySpan<CursorType> allowed = [CursorType.Loot, CursorType.Vendor];

        NpcNameTargeting.AcceptsCursor(allowed, CursorType.Skin).Should().BeFalse();
    }
}
