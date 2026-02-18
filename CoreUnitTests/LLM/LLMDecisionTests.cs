using Core.LLM;
using FluentAssertions;
using System;
using System.Collections.Generic;
using Xunit;

namespace CoreUnitTests.LLM;

/// <summary>
/// Tests for the LLMDecision record.
/// </summary>
public class LLMDecisionTests
{
    #region Construction Tests

    [Fact]
    public void LLMDecision_CreateWithValues_StoresCorrectly()
    {
        // Arrange & Act
        var decision = new LLMDecision(
            SuggestedAction: "AttackTarget",
            Reasoning: "Target is low health",
            Confidence: 0.85f,
            Metadata: null
        );

        // Assert
        decision.SuggestedAction.Should().Be("AttackTarget");
        decision.Reasoning.Should().Be("Target is low health");
        decision.Confidence.Should().Be(0.85f);
        decision.Metadata.Should().BeNull();
    }

    [Fact]
    public void LLMDecision_WithMetadata_StoresCorrectly()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            ["TargetHealth"] = 25,
            ["PlayerHealth"] = 80
        };

        // Act
        var decision = new LLMDecision(
            SuggestedAction: "Heal",
            Reasoning: "Player health is critical",
            Confidence: 0.92f,
            Metadata: metadata
        );

        // Assert
        decision.Metadata.Should().NotBeNull();
        decision.Metadata.Should().ContainKey("TargetHealth");
        decision.Metadata!["TargetHealth"].Should().Be(25);
    }

    #endregion

    #region Confidence Tests

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.5f)]
    [InlineData(1.0f)]
    [InlineData(0.99f)]
    public void LLMDecision_VariousConfidenceValues_Accepted(float confidence)
    {
        // Arrange & Act
        var decision = new LLMDecision("Action", "Reason", confidence, null);

        // Assert
        decision.Confidence.Should().Be(confidence);
    }

    [Fact]
    public void LLMDecision_MaxConfidence_StoresCorrectly()
    {
        // Arrange & Act
        var decision = new LLMDecision("Action", "Reason", 1.0f, null);

        // Assert
        decision.Confidence.Should().Be(1.0f);
    }

    [Fact]
    public void LLMDecision_MinConfidence_StoresCorrectly()
    {
        // Arrange & Act
        var decision = new LLMDecision("Action", "Reason", 0.0f, null);

        // Assert
        decision.Confidence.Should().Be(0.0f);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void LLMDecision_Equality_SameValues_AreEqual()
    {
        // Arrange
        var decision1 = new LLMDecision("Attack", "Low health", 0.8f, null);
        var decision2 = new LLMDecision("Attack", "Low health", 0.8f, null);

        // Assert
        decision1.Should().Be(decision2);
        decision1.GetHashCode().Should().Be(decision2.GetHashCode());
    }

    [Fact]
    public void LLMDecision_Equality_DifferentActions_AreNotEqual()
    {
        // Arrange
        var decision1 = new LLMDecision("Attack", "Reason", 0.8f, null);
        var decision2 = new LLMDecision("Flee", "Reason", 0.8f, null);

        // Assert
        decision1.Should().NotBe(decision2);
    }

    [Fact]
    public void LLMDecision_Equality_DifferentConfidence_AreNotEqual()
    {
        // Arrange
        var decision1 = new LLMDecision("Action", "Reason", 0.7f, null);
        var decision2 = new LLMDecision("Action", "Reason", 0.8f, null);

        // Assert
        decision1.Should().NotBe(decision2);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void LLMDecision_EmptyStrings_Allowed()
    {
        // Arrange & Act
        var decision = new LLMDecision("", "", 0.5f, null);

        // Assert
        decision.SuggestedAction.Should().BeEmpty();
        decision.Reasoning.Should().BeEmpty();
    }

    [Fact]
    public void LLMDecision_LongStrings_Accepted()
    {
        // Arrange
        var longAction = new string('A', 1000);
        var longReason = new string('B', 2000);

        // Act
        var decision = new LLMDecision(longAction, longReason, 0.5f, null);

        // Assert
        decision.SuggestedAction.Should().HaveLength(1000);
        decision.Reasoning.Should().HaveLength(2000);
    }

    [Fact]
    public void LLMDecision_EmptyMetadata_Allowed()
    {
        // Arrange
        var metadata = new Dictionary<string, object>();

        // Act
        var decision = new LLMDecision("Action", "Reason", 0.5f, metadata);

        // Assert
        decision.Metadata.Should().BeEmpty();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void LLMDecision_WithComplexMetadata_StoresCorrectly()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            ["Health"] = 50,
            ["Mana"] = 100.5,
            ["InCombat"] = true,
            ["LastAction"] = "CastSpell"
        };

        // Act
        var decision = new LLMDecision(
            SuggestedAction: "Cast Heal",
            Reasoning: "Health below threshold",
            Confidence: 0.95f,
            Metadata: metadata
        );

        // Assert
        decision.Metadata.Should().HaveCount(4);
        decision.Metadata!["InCombat"].Should().Be(true);
        decision.Metadata!["Mana"].Should().Be(100.5);
    }

    [Fact]
    public void LLMDecision_Clone_WithMutation_CreatesNewInstance()
    {
        // Arrange
        var original = new LLMDecision("Attack", "Low health", 0.8f, null);

        // Act
        var modified = original with { Confidence = 0.9f };

        // Assert
        modified.Should().NotBe(original);
        modified.Confidence.Should().Be(0.9f);
        modified.SuggestedAction.Should().Be(original.SuggestedAction);
    }

    #endregion
}
