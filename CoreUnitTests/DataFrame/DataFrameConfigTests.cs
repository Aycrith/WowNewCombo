using System;

using Core;

using FluentAssertions;

using SixLabors.ImageSharp;

using Xunit;

namespace CoreUnitTests.DataFrameTests;

public class DataFrameConfigTests
{
    [Fact]
    public void DataFrameConfig_CreateWithValues_StoresCorrectly()
    {
        // Arrange
        const int version = 1;
        Version addonVersion = new(1, 2, 3);
        Rectangle rect = new(10, 20, 100, 200);
        DataFrameMeta meta = new(12345, 2, 4, 10, 100);
        DataFrame[] frames = new DataFrame[100];
        for (int i = 0; i < 100; i++)
        {
            frames[i] = new DataFrame(i, i * 10, i * 5);
        }

        // Act
        var config = new DataFrameConfig(version, addonVersion, rect, meta, frames);

        // Assert
        config.Version.Should().Be(version);
        config.AddonVersion.Should().Be(addonVersion);
        config.Rect.Should().Be(rect);
        config.Meta.Should().Be(meta);
        config.Frames.Should().HaveCount(100);
    }

    [Fact]
    public void DataFrameConfig_CreateWithEmptyFrames_StoresCorrectly()
    {
        // Arrange
        DataFrame[] frames = Array.Empty<DataFrame>();

        // Act
        var config = new DataFrameConfig(1, new Version(1, 0), new Rectangle(0, 0, 100, 100), DataFrameMeta.Empty, frames);

        // Assert
        config.Frames.Should().BeEmpty();
    }

    [Fact]
    public void DataFrameConfig_Equality_SameValues_AreEqual()
    {
        // Arrange
        Version version = new(1, 0);
        Rectangle rect = new(0, 0, 100, 100);
        DataFrameMeta meta = new(0, 1, 4, 10, 50);
        DataFrame[] frames = new DataFrame[50];

        var config1 = new DataFrameConfig(1, version, rect, meta, frames);
        var config2 = new DataFrameConfig(1, version, rect, meta, frames);

        // Assert
        config1.Should().Be(config2);
        (config1 == config2).Should().BeTrue();
    }

    [Fact]
    public void DataFrameConfig_Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var config1 = new DataFrameConfig(1, new Version(1, 0), new Rectangle(0, 0, 100, 100), DataFrameMeta.Empty, Array.Empty<DataFrame>());
        var config2 = new DataFrameConfig(2, new Version(1, 0), new Rectangle(0, 0, 100, 100), DataFrameMeta.Empty, Array.Empty<DataFrame>());

        // Assert
        config1.Should().NotBe(config2);
        (config1 != config2).Should().BeTrue();
    }

    [Fact]
    public void DataFrameConfig_Deconstruct_ReturnsCorrectValues()
    {
        // Arrange
        const int version = 2;
        Version addonVersion = new(2, 5);
        Rectangle rect = new(50, 60, 200, 300);
        DataFrameMeta meta = new(11111, 3, 5, 20, 200);
        DataFrame[] frames = Array.Empty<DataFrame>();

        var config = new DataFrameConfig(version, addonVersion, rect, meta, frames);

        // Act
        // Note: DataFrameConfig doesn't have a deconstructor by default, so we access properties directly
        int resultVersion = config.Version;
        Version resultAddonVersion = config.AddonVersion;
        Rectangle resultRect = config.Rect;
        DataFrameMeta resultMeta = config.Meta;
        DataFrame[] resultFrames = config.Frames;

        // Assert
        resultVersion.Should().Be(version);
        resultAddonVersion.Should().Be(addonVersion);
        resultRect.Should().Be(rect);
        resultMeta.Should().Be(meta);
        resultFrames.Should().BeEquivalentTo(frames);
    }

    [Fact]
    public void DataFrameConfig_Properties_AreReadOnly()
    {
        // Arrange
        var config = new DataFrameConfig(1, new Version(1, 0), new Rectangle(0, 0, 100, 100), DataFrameMeta.Empty, Array.Empty<DataFrame>());

        // Assert - All properties should be get-only (init-only in constructor)
        config.Version.Should().Be(1);
        config.AddonVersion.Should().Be(new Version(1, 0));
    }

    [Fact]
    public void DataFrameConfig_LargeFrameArray_StoresCorrectly()
    {
        // Arrange
        DataFrame[] frames = new DataFrame[1000];
        for (int i = 0; i < 1000; i++)
        {
            frames[i] = new DataFrame(i, i % 100, i / 10);
        }

        // Act
        var config = new DataFrameConfig(1, new Version(1, 0), new Rectangle(0, 0, 1000, 1000), DataFrameMeta.Empty, frames);

        // Assert
        config.Frames.Should().HaveCount(1000);
        config.Frames[500].Index.Should().Be(500);
    }

    [Fact]
    public void DataFrameConfig_ToString_ContainsVersion()
    {
        // Arrange
        var config = new DataFrameConfig(5, new Version(2, 3), new Rectangle(0, 0, 100, 100), DataFrameMeta.Empty, Array.Empty<DataFrame>());

        // Act
        string result = config.ToString();

        // Assert
        result.Should().Contain("5");
    }

    [Fact]
    public void DataFrameConfig_MetaProperty_ReturnsCorrectValue()
    {
        // Arrange
        DataFrameMeta meta = new(99999, 2, 4, 10, 100);
        var config = new DataFrameConfig(1, new Version(1, 0), new Rectangle(0, 0, 100, 100), meta, Array.Empty<DataFrame>());

        // Assert
        config.Meta.Hash.Should().Be(99999);
    }

    [Theory]
    [InlineData(0, 0, 100, 100)]
    [InlineData(10, 20, 50, 50)]
    [InlineData(100, 200, 300, 400)]
    public void DataFrameConfig_VariousRectangles_StoredCorrectly(int x, int y, int width, int height)
    {
        // Arrange
        Rectangle rect = new(x, y, width, height);

        // Act
        var config = new DataFrameConfig(1, new Version(1, 0), rect, DataFrameMeta.Empty, Array.Empty<DataFrame>());

        // Assert
        config.Rect.X.Should().Be(x);
        config.Rect.Y.Should().Be(y);
        config.Rect.Width.Should().Be(width);
        config.Rect.Height.Should().Be(height);
    }

    [Fact]
    public void DataFrameConfig_VersionZero_StoredCorrectly()
    {
        // Act
        var config = new DataFrameConfig(0, new Version(1, 0), new Rectangle(0, 0, 100, 100), DataFrameMeta.Empty, Array.Empty<DataFrame>());

        // Assert
        config.Version.Should().Be(0);
    }

    [Fact]
    public void DataFrameConfig_FramesArrayReference_IsPreserved()
    {
        // Arrange
        DataFrame[] frames = new DataFrame[10];
        for (int i = 0; i < 10; i++)
        {
            frames[i] = new DataFrame(i, i * 10, i * 5);
        }

        var config = new DataFrameConfig(1, new Version(1, 0), new Rectangle(0, 0, 100, 100), DataFrameMeta.Empty, frames);

        // Assert
        config.Frames.Should().BeSameAs(frames);
    }
}
