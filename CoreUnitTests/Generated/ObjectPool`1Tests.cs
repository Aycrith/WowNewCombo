using FluentAssertions;
using System;
using System.Collections.Generic;
using Xunit;

namespace CoreUnitTests;

/// <summary>
/// Generated test suite for ObjectPool`1
/// Coverage: 0% - Auto-generated stub
/// </summary>
public class ObjectPool`1Tests
{

    #region GetCount (1)

    [Fact]
    public void GetCount_HappyPath_ReturnsExpected()
    {
        // Arrange
        // TODO: Setup instance
        var instance = new ObjectPool`1();

        // Act
        // TODO: Call get_Count
        var result = true;

        // Assert
        // TODO: Verify expected behavior
        result.Should().BeTrue();
    }

    #endregion

    #region GetMaxsize (2)

    [Fact]
    public void GetMaxsize_HappyPath_ReturnsExpected()
    {
        // Arrange
        // TODO: Setup instance
        var instance = new ObjectPool`1();

        // Act
        // TODO: Call get_MaxSize
        var result = true;

        // Assert
        // TODO: Verify expected behavior
        result.Should().BeTrue();
    }

    #endregion

    #region GetRentcount (3)

    [Fact]
    public void GetRentcount_HappyPath_ReturnsExpected()
    {
        // Arrange
        // TODO: Setup instance
        var instance = new ObjectPool`1();

        // Act
        // TODO: Call get_RentCount
        var result = true;

        // Assert
        // TODO: Verify expected behavior
        result.Should().BeTrue();
    }

    #endregion

    #region GetReturncount (4)

    [Fact]
    public void GetReturncount_HappyPath_ReturnsExpected()
    {
        // Arrange
        // TODO: Setup instance
        var instance = new ObjectPool`1();

        // Act
        // TODO: Call get_ReturnCount
        var result = true;

        // Assert
        // TODO: Verify expected behavior
        result.Should().BeTrue();
    }

    #endregion

    #region GetCreatecount (5)

    [Fact]
    public void GetCreatecount_HappyPath_ReturnsExpected()
    {
        // Arrange
        // TODO: Setup instance
        var instance = new ObjectPool`1();

        // Act
        // TODO: Call get_CreateCount
        var result = true;

        // Assert
        // TODO: Verify expected behavior
        result.Should().BeTrue();
    }

    #endregion

    #region GetDiscardcount (6)

    [Fact]
    public void GetDiscardcount_HappyPath_ReturnsExpected()
    {
        // Arrange
        // TODO: Setup instance
        var instance = new ObjectPool`1();

        // Act
        // TODO: Call get_DiscardCount
        var result = true;

        // Assert
        // TODO: Verify expected behavior
        result.Should().BeTrue();
    }

    #endregion

    #region GetHitrate (7)

    [Fact]
    public void GetHitrate_HappyPath_ReturnsExpected()
    {
        // Arrange
        // TODO: Setup instance
        var instance = new ObjectPool`1();

        // Act
        // TODO: Call get_HitRate
        var result = true;

        // Assert
        // TODO: Verify expected behavior
        result.Should().BeTrue();
    }

    #endregion

    #region Rent (8)

    [Fact]
    public void Rent_HappyPath_ReturnsExpected()
    {
        // Arrange
        // TODO: Setup test dependencies
        var instance = new ObjectPool`1();

        // Act
        // TODO: Call Rent
        var result = true;

        // Assert
        // TODO: Verify expected behavior
        result.Should().BeTrue();
    }

    #endregion

    #region Return (9)

    [Fact]
    public void Return_HappyPath_ReturnsExpected()
    {
        // Arrange
        // TODO: Setup test dependencies
        var instance = new ObjectPool`1();

        // Parameters:
        // param1 = null; // T

        // Act
        // TODO: Call Return
        var result = true;

        // Assert
        // TODO: Verify expected behavior
        result.Should().BeTrue();
    }

    [Fact]
    public void Return_InvalidInput_HandlesGracefully()
    {
        // Arrange
        // TODO: Setup invalid input scenario
        var instance = new ObjectPool`1();

        // Act & Assert
        // TODO: Verify exception handling or error case
        // Assert.Throws<Exception>(() => instance.Return());
    }

    #endregion

    #region Clear (10)

    [Fact]
    public void Clear_HappyPath_ReturnsExpected()
    {
        // Arrange
        // TODO: Setup test dependencies
        var instance = new ObjectPool`1();

        // Act
        // TODO: Call Clear
        var result = true;

        // Assert
        // TODO: Verify expected behavior
        result.Should().BeTrue();
    }

    #endregion

    // NOTE: Only first 10 of 13 methods generated
    // Add more tests manually or increase MaxStubsPerClass

}

