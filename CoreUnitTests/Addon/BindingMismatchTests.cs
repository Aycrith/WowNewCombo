using System;

using Core;

using FluentAssertions;

using SharedLib;

using Xunit;

namespace CoreUnitTests.Addon;

public class BindingMismatchTests
{
    [Fact]
    public void BindingMismatch_CreateWithValues_StoresCorrectly()
    {
        // Arrange
        const BindingID bindingId = BindingID.MOVEFORWARD;
        const ConsoleKey expectedKey = ConsoleKey.W;
        const ModifierKey expectedModifier = ModifierKey.None;
        const ConsoleKey actualKey = ConsoleKey.S;
        const ModifierKey actualModifier = ModifierKey.None;

        // Act
        var mismatch = new BindingMismatch(bindingId, expectedKey, expectedModifier, actualKey, actualModifier);

        // Assert
        mismatch.BindingId.Should().Be(bindingId);
        mismatch.ExpectedKey.Should().Be(expectedKey);
        mismatch.ExpectedModifier.Should().Be(expectedModifier);
        mismatch.ActualKey.Should().Be(actualKey);
        mismatch.ActualModifier.Should().Be(actualModifier);
    }

    [Fact]
    public void BindingMismatch_CreateWithModifier_StoresCorrectly()
    {
        // Act
        var mismatch = new BindingMismatch(
            BindingID.JUMP,
            ConsoleKey.Spacebar,
            ModifierKey.Shift,
            ConsoleKey.Spacebar,
            ModifierKey.None);

        // Assert
        mismatch.ExpectedModifier.Should().Be(ModifierKey.Shift);
        mismatch.ActualModifier.Should().Be(ModifierKey.None);
    }

    [Fact]
    public void BindingMismatch_With_ModifiesSingleProperty()
    {
        // Arrange
        var original = new BindingMismatch(BindingID.MOVEBACKWARD, ConsoleKey.S, ModifierKey.None, ConsoleKey.X, ModifierKey.None);

        // Act
        var modified = original with { ActualKey = ConsoleKey.S };

        // Assert
        modified.ActualKey.Should().Be(ConsoleKey.S);
        modified.BindingId.Should().Be(original.BindingId);
        modified.ExpectedKey.Should().Be(original.ExpectedKey);
    }

    [Fact]
    public void BindingMismatch_Equality_SameValues_AreEqual()
    {
        // Arrange
        var mismatch1 = new BindingMismatch(BindingID.TURNLEFT, ConsoleKey.A, ModifierKey.None, ConsoleKey.Q, ModifierKey.None);
        var mismatch2 = new BindingMismatch(BindingID.TURNLEFT, ConsoleKey.A, ModifierKey.None, ConsoleKey.Q, ModifierKey.None);

        // Assert
        mismatch1.Should().Be(mismatch2);
        (mismatch1 == mismatch2).Should().BeTrue();
    }

    [Fact]
    public void BindingMismatch_Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var mismatch1 = new BindingMismatch(BindingID.TURNRIGHT, ConsoleKey.D, ModifierKey.None, ConsoleKey.E, ModifierKey.None);
        var mismatch2 = new BindingMismatch(BindingID.TURNRIGHT, ConsoleKey.D, ModifierKey.None, ConsoleKey.R, ModifierKey.None);

        // Assert
        mismatch1.Should().NotBe(mismatch2);
        (mismatch1 != mismatch2).Should().BeTrue();
    }

    [Fact]
    public void BindingMismatch_Deconstruct_ReturnsCorrectValues()
    {
        // Arrange
        var mismatch = new BindingMismatch(BindingID.JUMP, ConsoleKey.Spacebar, ModifierKey.None, ConsoleKey.Z, ModifierKey.Shift);

        // Act
        var (bindingId, expectedKey, expectedModifier, actualKey, actualModifier) = mismatch;

        // Assert
        bindingId.Should().Be(BindingID.JUMP);
        expectedKey.Should().Be(ConsoleKey.Spacebar);
        expectedModifier.Should().Be(ModifierKey.None);
        actualKey.Should().Be(ConsoleKey.Z);
        actualModifier.Should().Be(ModifierKey.Shift);
    }

    [Theory]
    [InlineData(BindingID.MOVEFORWARD)]
    [InlineData(BindingID.MOVEBACKWARD)]
    [InlineData(BindingID.TARGETNEARESTENEMY)]
    [InlineData(BindingID.JUMP)]
    public void BindingMismatch_VariousBindingIds_Accepted(BindingID bindingId)
    {
        // Act
        var mismatch = new BindingMismatch(bindingId, ConsoleKey.A, ModifierKey.None, ConsoleKey.B, ModifierKey.None);

        // Assert
        mismatch.BindingId.Should().Be(bindingId);
    }

    [Fact]
    public void BindingMismatch_ToString_ContainsBindingId()
    {
        // Arrange
        var mismatch = new BindingMismatch(BindingID.STRAFELEFT, ConsoleKey.Q, ModifierKey.None, ConsoleKey.E, ModifierKey.None);

        // Act
        string result = mismatch.ToString();

        // Assert
        result.Should().Contain("STRAFELEFT");
    }

    [Fact]
    public void BindingMismatch_CompleteMismatch_DifferentKeyAndModifier()
    {
        // Arrange & Act
        var mismatch = new BindingMismatch(
            BindingID.SITORSTAND,
            ConsoleKey.X,
            ModifierKey.None,
            ConsoleKey.C,
            ModifierKey.Shift);

        // Assert
        mismatch.ExpectedKey.Should().NotBe(mismatch.ActualKey);
        mismatch.ExpectedModifier.Should().NotBe(mismatch.ActualModifier);
    }

    [Fact]
    public void BindingMismatch_AltModifier_StoresCorrectly()
    {
        // Act
        var mismatch = new BindingMismatch(
            BindingID.ASSISTTARGET,
            ConsoleKey.F,
            ModifierKey.Alt,
            ConsoleKey.F,
            ModifierKey.None);

        // Assert
        mismatch.ExpectedModifier.Should().Be(ModifierKey.Alt);
        mismatch.ActualModifier.Should().Be(ModifierKey.None);
    }
}
