using Core.Testing;
using FluentAssertions;
using System;
using System.Collections.Generic;
using Xunit;

namespace CoreUnitTests.Testing;

/// <summary>
/// Tests for the TestResult and TestCheck records.
/// </summary>
public class TestResultTests
{
    #region TestResult Creation Tests

    [Fact]
    public void TestResult_CreateWithValues_StoresCorrectly()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var duration = TimeSpan.FromSeconds(5);
        var checks = new List<TestCheck> { TestCheck.Pass("Check1", "Value1") };

        // Act
        var result = new TestResult(
            Success: true,
            TestName: "TestMethod",
            Timestamp: timestamp,
            Duration: duration,
            Checks: checks,
            Data: null,
            Error: null,
            Message: "All good"
        );

        // Assert
        result.Success.Should().BeTrue();
        result.TestName.Should().Be("TestMethod");
        result.Timestamp.Should().Be(timestamp);
        result.Duration.Should().Be(duration);
        result.Checks.Should().HaveCount(1);
        result.Message.Should().Be("All good");
    }

    [Fact]
    public void TestResult_PassFactory_CreatesSuccessfulResult()
    {
        // Arrange
        var checks = new List<TestCheck>
        {
            TestCheck.Pass("Check1", "Expected1"),
            TestCheck.Pass("Check2", "Expected2")
        };

        // Act
        var result = TestResult.Pass(
            testName: "MyTest",
            duration: TimeSpan.FromSeconds(3),
            checks: checks,
            message: "Test completed successfully"
        );

        // Assert
        result.Success.Should().BeTrue();
        result.TestName.Should().Be("MyTest");
        result.Duration.Should().Be(TimeSpan.FromSeconds(3));
        result.Checks.Should().HaveCount(2);
        result.Error.Should().BeNull();
        result.Message.Should().Be("Test completed successfully");
    }

    [Fact]
    public void TestResult_FailFactory_CreatesFailedResult()
    {
        // Arrange
        var checks = new List<TestCheck> { TestCheck.Fail("Check1", "Expected", "Actual", "Mismatch") };

        // Act
        var result = TestResult.Fail(
            testName: "FailingTest",
            duration: TimeSpan.FromSeconds(1),
            checks: checks,
            error: "Assertion failed",
            data: null
        );

        // Assert
        result.Success.Should().BeFalse();
        result.TestName.Should().Be("FailingTest");
        result.Error.Should().Be("Assertion failed");
        result.Message.Should().BeNull();
    }

    [Fact]
    public void TestResult_FromException_CreatesErrorResult()
    {
        // Arrange
        var exception = new InvalidOperationException("Something went wrong");

        // Act
        var result = TestResult.FromException(
            testName: "ExceptionTest",
            duration: TimeSpan.FromMilliseconds(100),
            ex: exception
        );

        // Assert
        result.Success.Should().BeFalse();
        result.TestName.Should().Be("ExceptionTest");
        result.Error.Should().Contain("InvalidOperationException");
        result.Error.Should().Contain("Something went wrong");
    }

    [Fact]
    public void TestResult_WithData_StoresDataCorrectly()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["Health"] = 100,
            ["Mana"] = 50
        };

        // Act
        var result = TestResult.Pass(
            testName: "DataTest",
            duration: TimeSpan.Zero,
            checks: [],
            data: data
        );

        // Assert
        result.Data.Should().NotBeNull();
        result.Data.Should().ContainKey("Health");
        result.Data!["Health"].Should().Be(100);
    }

    #endregion

    #region TestCheck Tests

    [Fact]
    public void TestCheck_PassFactory_CreatesPassingCheck()
    {
        // Act
        var check = TestCheck.Pass("ValueCheck", "ExpectedValue", "Values match");

        // Assert
        check.Name.Should().Be("ValueCheck");
        check.Passed.Should().BeTrue();
        check.Expected.Should().Be("ExpectedValue");
        check.Actual.Should().Be("ExpectedValue");
        check.Message.Should().Be("Values match");
    }

    [Fact]
    public void TestCheck_FailFactory_CreatesFailingCheck()
    {
        // Act
        var check = TestCheck.Fail("ValueCheck", "Expected", "Actual", "Mismatch detected");

        // Assert
        check.Name.Should().Be("ValueCheck");
        check.Passed.Should().BeFalse();
        check.Expected.Should().Be("Expected");
        check.Actual.Should().Be("Actual");
        check.Message.Should().Be("Mismatch detected");
    }

    [Fact]
    public void TestCheck_Equality_SameValues_AreEqual()
    {
        // Arrange
        var check1 = new TestCheck("Name", true, "Exp", "Act", "Msg");
        var check2 = new TestCheck("Name", true, "Exp", "Act", "Msg");

        // Assert
        check1.Should().Be(check2);
    }

    [Fact]
    public void TestCheck_Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var check1 = new TestCheck("Name1", true, "Exp", "Act", "Msg");
        var check2 = new TestCheck("Name2", true, "Exp", "Act", "Msg");

        // Assert
        check1.Should().NotBe(check2);
    }

    [Fact]
    public void TestCheck_NullMessage_Allowed()
    {
        // Act
        var check = new TestCheck("Name", true, "Exp", "Act", null);

        // Assert
        check.Message.Should().BeNull();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void TestResult_WithMultipleChecks_StoresAllChecks()
    {
        // Arrange
        var checks = new List<TestCheck>
        {
            TestCheck.Pass("HealthCheck", "100"),
            TestCheck.Pass("ManaCheck", "50"),
            TestCheck.Fail("LevelCheck", "25", "24", "Level mismatch")
        };

        // Act
        var result = TestResult.Fail(
            testName: "MultipleChecks",
            duration: TimeSpan.FromSeconds(2),
            checks: checks,
            error: "Some checks failed"
        );

        // Assert
        result.Checks.Should().HaveCount(3);
        result.Checks[0].Passed.Should().BeTrue();
        result.Checks[1].Passed.Should().BeTrue();
        result.Checks[2].Passed.Should().BeFalse();
    }

    [Fact]
    public void TestResult_EmptyChecks_Allowed()
    {
        // Act
        var result = TestResult.Pass(
            testName: "EmptyTest",
            duration: TimeSpan.Zero,
            checks: []
        );

        // Assert
        result.Checks.Should().BeEmpty();
    }

    [Fact]
    public void TestResult_ZeroDuration_Allowed()
    {
        // Act
        var result = TestResult.Pass(
            testName: "InstantTest",
            duration: TimeSpan.Zero,
            checks: []
        );

        // Assert
        result.Duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void TestResult_Timestamp_IsUtc()
    {
        // Act
        var before = DateTime.UtcNow.AddMilliseconds(-100);
        var result = TestResult.Pass("TimingTest", TimeSpan.Zero, []);
        var after = DateTime.UtcNow.AddMilliseconds(100);

        // Assert
        result.Timestamp.Should().BeOnOrAfter(before);
        result.Timestamp.Should().BeOnOrBefore(after);
    }

    #endregion
}
