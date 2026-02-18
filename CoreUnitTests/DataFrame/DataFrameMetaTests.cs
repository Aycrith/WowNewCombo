using System;

using Core;

using FluentAssertions;

using SixLabors.ImageSharp;

using Xunit;

namespace CoreUnitTests.DataFrameTests;

public class DataFrameMetaTests
{
    [Fact]
    public void DataFrameMeta_CreateWithValues_StoresCorrectly()
    {
        // Arrange & Act
        var meta = new DataFrameMeta(12345, 2, 4, 10, 100);

        // Assert
        meta.Hash.Should().Be(12345);
        meta.Spacing.Should().Be(2);
        meta.Sizes.Should().Be(4);
        meta.Rows.Should().Be(10);
        meta.Count.Should().Be(100);
    }

    [Fact]
    public void DataFrameMeta_CreateWithZeroValues_StoresCorrectly()
    {
        // Act
        var meta = new DataFrameMeta(0, 0, 0, 0, 0);

        // Assert
        meta.Hash.Should().Be(0);
        meta.Spacing.Should().Be(0);
        meta.Sizes.Should().Be(0);
        meta.Rows.Should().Be(0);
        meta.Count.Should().Be(0);
    }

    [Fact]
    public void DataFrameMeta_CreateWithNegativeHash_StoresCorrectly()
    {
        // Act
        var meta = new DataFrameMeta(-1, 2, 4, 10, 100);

        // Assert
        meta.Hash.Should().Be(-1);
    }

    [Fact]
    public void DataFrameMeta_Empty_StaticProperty_ReturnsEmptyInstance()
    {
        // Act
        ref readonly DataFrameMeta empty = ref DataFrameMeta.Empty;

        // Assert
        empty.Hash.Should().Be(-1);
        empty.Spacing.Should().Be(0);
        empty.Sizes.Should().Be(0);
        empty.Rows.Should().Be(0);
        empty.Count.Should().Be(0);
    }

    [Fact]
    public void DataFrameMeta_Equality_SameValues_AreEqual()
    {
        // Arrange
        var meta1 = new DataFrameMeta(123, 2, 4, 10, 50);
        var meta2 = new DataFrameMeta(123, 2, 4, 10, 50);

        // Assert
        meta1.Should().Be(meta2);
        (meta1 == meta2).Should().BeTrue();
        meta1.GetHashCode().Should().Be(meta2.GetHashCode());
    }

    [Fact]
    public void DataFrameMeta_Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var meta1 = new DataFrameMeta(123, 2, 4, 10, 50);
        var meta2 = new DataFrameMeta(456, 2, 4, 10, 50);

        // Assert
        meta1.Should().NotBe(meta2);
        (meta1 != meta2).Should().BeTrue();
    }

    [Fact]
    public void DataFrameMeta_EstimatedSize_ValidParameters_ReturnsSize()
    {
        // Arrange
        var meta = new DataFrameMeta(0, 1, 4, 2, 10); // 2 rows, 10 count
        Rectangle screenRect = new(0, 0, 1000, 1000);

        // Act
        Size result = meta.EstimatedSize(screenRect);

        // Assert
        result.Should().NotBe(Size.Empty);
        result.Width.Should().BeGreaterThan(0);
        result.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DataFrameMeta_EstimatedSize_ExceedsScreen_ReturnsEmpty()
    {
        // Arrange - very large frame count that won't fit
        var meta = new DataFrameMeta(0, 1, 100, 2, 1000); // Large cells
        Rectangle smallScreen = new(0, 0, 50, 50);

        // Act
        Size result = meta.EstimatedSize(smallScreen);

        // Assert
        result.Should().Be(Size.Empty);
    }

    [Fact]
    public void DataFrameMeta_EstimatedSize_FitsExactly_ReturnsCorrectSize()
    {
        // Arrange - Calculate exact fit
        // cellSize = 4 + 2 + (1 + 2) = 9 (with error margin of 2)
        var meta = new DataFrameMeta(0, 1, 4, 1, 10); // 1 row, 10 count
        Rectangle screenRect = new(0, 0, 100, 20);

        // Act
        Size result = meta.EstimatedSize(screenRect);

        // Assert
        result.Width.Should().BeGreaterThan(0);
        result.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DataFrameMeta_ToString_ContainsHash()
    {
        // Arrange
        var meta = new DataFrameMeta(99999, 2, 4, 10, 50);

        // Act
        string result = meta.ToString();

        // Assert
        result.Should().Contain("99999");
    }

    [Fact]
    public void DataFrameMeta_LargeValues_StoresCorrectly()
    {
        // Act
        var meta = new DataFrameMeta(int.MaxValue, 1000, 1000, 1000, 10000);

        // Assert
        meta.Hash.Should().Be(int.MaxValue);
        meta.Spacing.Should().Be(1000);
        meta.Sizes.Should().Be(1000);
        meta.Rows.Should().Be(1000);
        meta.Count.Should().Be(10000);
    }
}
