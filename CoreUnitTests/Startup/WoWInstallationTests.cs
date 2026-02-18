using Core.Startup;
using FluentAssertions;
using Xunit;

namespace CoreUnitTests.Startup;

/// <summary>
/// Tests for the WoWInstallation record.
/// </summary>
public class WoWInstallationTests
{
    #region Construction Tests

    [Fact]
    public void WoWInstallation_CreateWithValues_StoresCorrectly()
    {
        // Arrange & Act
        var install = new WoWInstallation(
            Path: @"C:\Games\WoW",
            ExecutablePath: @"C:\Games\WoW\WoW.exe",
            ExecutableName: "WoW.exe",
            Version: "1.14.0",
            HasDataToColorAddon: true,
            HasSecureButtonsXml: false
        );

        // Assert
        install.Path.Should().Be(@"C:\Games\WoW");
        install.ExecutablePath.Should().Be(@"C:\Games\WoW\WoW.exe");
        install.ExecutableName.Should().Be("WoW.exe");
        install.Version.Should().Be("1.14.0");
        install.HasDataToColorAddon.Should().BeTrue();
        install.HasSecureButtonsXml.Should().BeFalse();
    }

    [Fact]
    public void WoWInstallation_EmptyPaths_Allowed()
    {
        // Arrange & Act
        var install = new WoWInstallation("", "", "", "", false, false);

        // Assert
        install.Path.Should().BeEmpty();
        install.ExecutablePath.Should().BeEmpty();
        install.ExecutableName.Should().BeEmpty();
        install.Version.Should().BeEmpty();
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void WoWInstallation_Equality_SameValues_AreEqual()
    {
        // Arrange
        var install1 = new WoWInstallation(@"C:\WoW", @"C:\WoW\WoW.exe", "WoW.exe", "1.0", true, false);
        var install2 = new WoWInstallation(@"C:\WoW", @"C:\WoW\WoW.exe", "WoW.exe", "1.0", true, false);

        // Assert
        install1.Should().Be(install2);
        install1.GetHashCode().Should().Be(install2.GetHashCode());
    }

    [Fact]
    public void WoWInstallation_Equality_DifferentPaths_AreNotEqual()
    {
        // Arrange
        var install1 = new WoWInstallation(@"C:\WoW1", @"C:\WoW1\WoW.exe", "WoW.exe", "1.0", true, false);
        var install2 = new WoWInstallation(@"C:\WoW2", @"C:\WoW2\WoW.exe", "WoW.exe", "1.0", true, false);

        // Assert
        install1.Should().NotBe(install2);
    }

    [Fact]
    public void WoWInstallation_Equality_DifferentAddonFlags_AreNotEqual()
    {
        // Arrange
        var install1 = new WoWInstallation(@"C:\WoW", @"C:\WoW\WoW.exe", "WoW.exe", "1.0", true, false);
        var install2 = new WoWInstallation(@"C:\WoW", @"C:\WoW\WoW.exe", "WoW.exe", "1.0", false, false);

        // Assert
        install1.Should().NotBe(install2);
    }

    #endregion

    #region Deconstruction Tests

    [Fact]
    public void WoWInstallation_Deconstruct_ExtractsAllValues()
    {
        // Arrange
        var install = new WoWInstallation(@"C:\WoW", @"C:\WoW\WoW.exe", "WoW.exe", "1.14.0", true, false);

        // Act
        var (path, exePath, exeName, version, hasAddon, hasXml) = install;

        // Assert
        path.Should().Be(@"C:\WoW");
        exePath.Should().Be(@"C:\WoW\WoW.exe");
        exeName.Should().Be("WoW.exe");
        version.Should().Be("1.14.0");
        hasAddon.Should().BeTrue();
        hasXml.Should().BeFalse();
    }

    #endregion

    #region With Expression Tests

    [Fact]
    public void WoWInstallation_With_CreatesNewInstance()
    {
        // Arrange
        var original = new WoWInstallation(@"C:\WoW", @"C:\WoW\WoW.exe", "WoW.exe", "1.0", true, false);

        // Act
        var modified = original with { Version = "2.0" };

        // Assert
        modified.Should().NotBe(original);
        modified.Version.Should().Be("2.0");
        original.Version.Should().Be("1.0");
    }

    [Fact]
    public void WoWInstallation_With_PreservesOtherValues()
    {
        // Arrange
        var original = new WoWInstallation(@"C:\WoW", @"C:\WoW\WoW.exe", "WoW.exe", "1.0", true, false);

        // Act
        var modified = original with { HasDataToColorAddon = false };

        // Assert
        modified.Path.Should().Be(original.Path);
        modified.ExecutableName.Should().Be(original.ExecutableName);
        modified.HasDataToColorAddon.Should().BeFalse();
    }

    #endregion
}
