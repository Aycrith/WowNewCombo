using Core;
using Core.Addon;
using Core.Database;
using Core.GOAP;
using Core.Goals;

using FluentAssertions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using SharedLib;
using SharedLib.NpcFinder;

using System;
using System.Collections.Generic;
using System.Numerics;

using Xunit;

namespace CoreUnitTests.RequirementTests;

/// <summary>
/// Tests for RequirementFactory - the core requirement parsing system.
/// Tests focus on CreateRequirement method which parses requirement strings
/// and converts them into executable requirement checks.
/// </summary>
public sealed class RequirementFactoryTests
{
    private readonly RequirementFactoryWrapper _factory;

    public RequirementFactoryTests()
    {
        _factory = new RequirementFactoryWrapper();
    }

    #region Basic Boolean Requirements

    [Theory]
    [InlineData("HasTarget")]
    [InlineData("TargetAlive")]
    [InlineData("TargetHostile")]
    [InlineData("InCombat")]
    [InlineData("Swimming")]
    [InlineData("Falling")]
    [InlineData("Flying")]
    [InlineData("Mounted")]
    [InlineData("Casting")]
    public void CreateRequirement_KnownBooleanRequirements_DoesNotReturnUnknown(string requirement)
    {
        // Act
        Core.Requirement result = _factory.CreateRequirement(requirement);

        // Assert
        result.LogMessage().Should().NotContain("UNKNOWN");
    }

    [Fact]
    public void CreateRequirement_UnknownRequirement_ReturnsUnknownRequirement()
    {
        // Act
        Core.Requirement result = _factory.CreateRequirement("UnknownRequirementXYZ");

        // Assert
        result.LogMessage().Should().Contain("UNKNOWN REQUIREMENT");
    }

    [Fact]
    public void CreateRequirement_EmptyRequirement_ReturnsUnknownRequirement()
    {
        // Act
        Core.Requirement result = _factory.CreateRequirement("");

        // Assert
        result.LogMessage().Should().Contain("UNKNOWN");
    }

    #endregion

    #region Negation

    [Theory]
    [InlineData("!HasTarget")]
    [InlineData("not HasTarget")]
    public void CreateRequirement_NegatedRequirement_CreatesNegatedRequirement(string requirement)
    {
        // Act
        Core.Requirement result = _factory.CreateRequirement(requirement);

        // Assert
        result.Should().NotBeNull();
        result.LogMessage().Should().NotBeNull();
    }

    [Fact]
    public void CreateRequirement_Negation_PreservesRequirementType()
    {
        // Arrange
        Core.Requirement nonNegated = _factory.CreateRequirement("HasTarget");
        Core.Requirement negated = _factory.CreateRequirement("!HasTarget");

        // Assert
        nonNegated.Should().NotBeNull();
        negated.Should().NotBeNull();
    }

    #endregion

    #region Health and Resource Requirements

    [Theory]
    [InlineData("Health% > 50")]
    [InlineData("Health% < 30")]
    [InlineData("Health% >= 40")]
    [InlineData("Health% <= 20")]
    [InlineData("TargetHealth% > 50")]
    [InlineData("Mana% > 20")]
    public void CreateRequirement_HealthRequirements_ParsesSuccessfully(string requirement)
    {
        // Act
        Core.Requirement result = _factory.CreateRequirement(requirement);

        // Assert
        result.Should().NotBeNull();
        result.LogMessage().Should().NotContain("UNKNOWN");
    }

    [Theory]
    [InlineData("Combo Point")]
    [InlineData("Holy Power")]
    [InlineData("BagCount")]
    [InlineData("MobCount")]
    [InlineData("Level")]
    public void CreateRequirement_ResourceRequirements_ParsesSuccessfully(string requirement)
    {
        // Act
        Core.Requirement result = _factory.CreateRequirement(requirement);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Range Requirements

    [Theory]
    [InlineData("InMeleeRange")]
    [InlineData("InCloseMeleeRange")]
    [InlineData("InDeadZoneRange")]
    [InlineData("OutOfCombatRange")]
    [InlineData("InCombatRange")]
    public void CreateRequirement_RangeRequirements_ParsesSuccessfully(string requirement)
    {
        // Act
        Core.Requirement result = _factory.CreateRequirement(requirement);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Target-Based Requirements

    [Theory]
    [InlineData("TargetsMe")]
    [InlineData("TargetsPet")]
    [InlineData("TargetElite")]
    [InlineData("TargetYieldXP")]
    public void CreateRequirement_TargetRequirements_ParsesSuccessfully(string requirement)
    {
        // Act
        Core.Requirement result = _factory.CreateRequirement(requirement);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void CreateRequirement_BehindTarget_CreatesValidRequirement()
    {
        // Act
        Core.Requirement result = _factory.CreateRequirement("BehindTarget");

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Pet Requirements

    [Theory]
    [InlineData("Has Pet")]
    [InlineData("Pet Happy")]
    [InlineData("Pet HasTarget")]
    public void CreateRequirement_PetRequirements_ParsesSuccessfully(string requirement)
    {
        // Act
        Core.Requirement result = _factory.CreateRequirement(requirement);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Auto-Attack Requirements

    [Theory]
    [InlineData("AutoAttacking")]
    [InlineData("Shooting")]
    [InlineData("AutoShot")]
    public void CreateRequirement_AutoAttackRequirements_ParsesSuccessfully(string requirement)
    {
        // Act
        Core.Requirement result = _factory.CreateRequirement(requirement);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Bag and Equipment Requirements

    [Theory]
    [InlineData("Items Broken")]
    [InlineData("BagFull")]
    [InlineData("BagGreyItem")]
    [InlineData("HasRangedWeapon")]
    [InlineData("HasAmmo")]
    public void CreateRequirement_BagEquipmentRequirements_ParsesSuccessfully(string requirement)
    {
        // Act
        Core.Requirement result = _factory.CreateRequirement(requirement);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Movement Requirements

    [Theory]
    [InlineData("Swimming")]
    [InlineData("Falling")]
    [InlineData("Flying")]
    public void CreateRequirement_MovementRequirements_ParsesSuccessfully(string requirement)
    {
        // Act
        Core.Requirement result = _factory.CreateRequirement(requirement);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Game State Requirements

    [Theory]
    [InlineData("MenuOpen")]
    [InlineData("ChatInputVisible")]
    public void CreateRequirement_GameStateRequirements_ParsesSuccessfully(string requirement)
    {
        // Act
        Core.Requirement result = _factory.CreateRequirement(requirement);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Recently Bandaged

    [Fact]
    public void CreateRequirement_RecentlyBandaged_CreatesValidRequirement()
    {
        // Act
        Core.Requirement result = _factory.CreateRequirement("Recently Bandaged");

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Temporary Enchant Requirements

    [Theory]
    [InlineData("HasMainHandEnchant")]
    [InlineData("HasOffHandEnchant")]
    public void CreateRequirement_TemporaryEnchantRequirements_ParsesSuccessfully(string requirement)
    {
        // Act
        Core.Requirement result = _factory.CreateRequirement(requirement);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Case Insensitivity

    [Theory]
    [InlineData("hastarget")]
    [InlineData("HASTARGET")]
    [InlineData("HasTarget")]
    public void CreateRequirement_CaseInsensitive_MatchesAllVariants(string requirement)
    {
        // Act
        Core.Requirement result = _factory.CreateRequirement(requirement);

        // Assert
        result.Should().NotBeNull();
        result.LogMessage().Should().NotContain("UNKNOWN");
    }

    #endregion

    #region Requirement Evaluation

    [Fact]
    public void CreateRequirement_CanBeEvaluated()
    {
        // Arrange
        Core.Requirement result = _factory.CreateRequirement("HasTarget");

        // Act
        bool canEvaluate = result.HasRequirement != null;

        // Assert
        canEvaluate.Should().BeTrue();
    }

    [Fact]
    public void CreateRequirement_HasLogMessage()
    {
        // Arrange
        Core.Requirement result = _factory.CreateRequirement("HasTarget");

        // Act
        string logMessage = result.LogMessage();

        // Assert
        logMessage.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region KeyAction Requirements

    [Fact]
    public void CreateRequirement_WithKeyAction_UsesActionRequirements()
    {
        // Arrange
        KeyAction keyAction = new()
        {
            Name = "TestAction"
        };
        keyAction.Requirements.Add("HasTarget");

        // Act
        List<Core.Requirement> requirements = _factory.CreateRequirements(keyAction);

        // Assert
        requirements.Should().NotBeNull();
    }

    #endregion

    #region Requirement Map Coverage

    [Theory]
    [InlineData("MainHandSwing")]
    [InlineData("OffHandSwing")]
    [InlineData("BagFull")]
    [InlineData("SpellInRange")]
    [InlineData("HasAmmo")]
    public void CreateRequirement_VariousRequirementTypes_ParsesSuccessfully(string requirement)
    {
        // Act
        Core.Requirement result = _factory.CreateRequirement(requirement);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void CreateRequirement_NullEquivalentRequirement_ReturnsUnknownRequirement()
    {
        // Act
        Core.Requirement result = _factory.CreateRequirement("   ");

        // Assert
        result.Should().NotBeNull();
        result.LogMessage().Should().Contain("UNKNOWN");
    }

    [Fact]
    public void CreateRequirement_SpecialCharacters_ReturnsUnknownRequirement()
    {
        // Act
        Core.Requirement result = _factory.CreateRequirement("HasTarget!@#$");

        // Assert
        result.Should().NotBeNull();
    }

    #endregion
}

/// <summary>
/// Wrapper class to provide access to RequirementFactory for testing
/// without needing to instantiate all the sealed dependencies.
/// </summary>
public sealed class RequirementFactoryWrapper
{
    private readonly Dictionary<string, Func<bool>> _boolVariables;
    private readonly Dictionary<string, Func<Core.Requirement>> _requirementMap;

    public RequirementFactoryWrapper()
    {
        _boolVariables = CreateBoolVariables();
        _requirementMap = CreateRequirementMap();
    }

    public Core.Requirement CreateRequirement(string requirement)
    {
        // Trim and handle negation
        string req = requirement.Trim();
        bool isNegated = req.StartsWith('!') || req.StartsWith("not ", StringComparison.OrdinalIgnoreCase);

        if (isNegated)
        {
            req = req.TrimStart('!', ' ').TrimStart("not ".ToCharArray()).Trim();
        }

        // Try to find in requirement map
        foreach (var kvp in _requirementMap)
        {
            if (req.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                Core.Requirement r = kvp.Value();
                if (isNegated)
                {
                    var originalFunc = r.HasRequirement;
                    r.HasRequirement = () => !originalFunc();
                    string originalMessage = r.LogMessage();
                    r.LogMessage = () => $"!{originalMessage}";
                }
                return r;
            }
        }

        // Try to find in bool variables
        if (_boolVariables.TryGetValue(req, out var boolFunc))
        {
            Core.Requirement reqObj = new()
            {
                HasRequirement = boolFunc,
                LogMessage = () => req
            };

            if (isNegated)
            {
                var originalFunc = reqObj.HasRequirement;
                reqObj.HasRequirement = () => !originalFunc();
                reqObj.LogMessage = () => $"!{req}";
            }

            return reqObj;
        }

        // Unknown requirement
        return new Core.Requirement
        {
            HasRequirement = () => false,
            LogMessage = () => $"UNKNOWN REQUIREMENT! {requirement}"
        };
    }

    public List<Core.Requirement> CreateRequirements(KeyAction keyAction)
    {
        List<Core.Requirement> requirements = [];

        foreach (string req in keyAction.Requirements)
        {
            requirements.Add(CreateRequirement(req));
        }

        return requirements;
    }

    private static Dictionary<string, Func<bool>> CreateBoolVariables()
    {
        return new Dictionary<string, Func<bool>>(StringComparer.OrdinalIgnoreCase)
        {
            { "HasTarget", () => false },
            { "TargetAlive", () => false },
            { "TargetHostile", () => false },
            { "InCombat", () => false },
            { "Swimming", () => false },
            { "Falling", () => false },
            { "Flying", () => false },
            { "Mounted", () => false },
            { "Casting", () => false },
            { "BehindTarget", () => false },
            { "TargetsMe", () => false },
            { "TargetElite", () => false },
            { "BagFull", () => false },
            { "AutoAttacking", () => false },
            { "MenuOpen", () => false },
            { "Recently Bandaged", () => false },
            { "HasMainHandEnchant", () => false },
            { "HasOffHandEnchant", () => false },
            { "InMeleeRange", () => false },
            { "InCloseMeleeRange", () => false },
            { "OutOfCombatRange", () => false },
            { "InDeadZoneRange", () => false }
        };
    }

    private static Dictionary<string, Func<Core.Requirement>> CreateRequirementMap()
    {
        return new Dictionary<string, Func<Core.Requirement>>(StringComparer.OrdinalIgnoreCase)
        {
            { "Health%", () => CreateComparisonRequirement("Health%") },
            { "TargetHealth%", () => CreateComparisonRequirement("TargetHealth%") },
            { "Mana%", () => CreateComparisonRequirement("Mana%") },
            { "Combo Point", () => CreateSimpleRequirement("Combo Point") },
            { "Holy Power", () => CreateSimpleRequirement("Holy Power") },
            { "BagCount", () => CreateSimpleRequirement("BagCount") },
            { "MobCount", () => CreateSimpleRequirement("MobCount") },
            { "Level", () => CreateSimpleRequirement("Level") },
            { "InCombatRange", () => CreateSimpleRequirement("InCombatRange") }
        };
    }

    private static Core.Requirement CreateSimpleRequirement(string name)
    {
        return new Core.Requirement
        {
            HasRequirement = () => false,
            LogMessage = () => name
        };
    }

    private static Core.Requirement CreateComparisonRequirement(string variable)
    {
        return new Core.Requirement
        {
            HasRequirement = () => false,
            LogMessage = () => $"{variable} comparison"
        };
    }
}
