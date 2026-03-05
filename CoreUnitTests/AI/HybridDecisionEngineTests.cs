using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

using Core;
using Core.AI.HybridDecision;
using Core.Database;
using Core.FeatureFlags;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using SharedLib;

using Xunit;

namespace CoreUnitTests.AI;

/// <summary>
/// Tests for HybridDecisionEngine.DetectUnexpectedState logic.
/// </summary>
public sealed class HybridDecisionEngineTests
{
    [Fact]
    public void DetectUnexpectedState_LowHealthOutOfCombat_ReturnsTrue()
    {
        // Arrange — 5% health, no combat flag set
        FakeAddonDataProvider provider = new();
        provider.Data[10] = 100; // HealthMax
        provider.Data[11] = 5;   // HealthCurrent -> ~5%

        AddonBits bits = new();
        // bits.Combat() == false by default (no Update called)

        HybridDecisionEngine engine = CreateEngine(provider, bits);

        // Act
        bool result = InvokeDetectUnexpectedState(engine);

        // Assert
        result.Should().BeTrue(
            "critically low health outside of combat is an unexpected state requiring LLM intervention");
    }

    [Fact]
    public void DetectUnexpectedState_FullHealthOutOfCombat_ReturnsFalse()
    {
        // Arrange — 100% health, no combat
        FakeAddonDataProvider provider = new();
        provider.Data[10] = 100;
        provider.Data[11] = 100; // HealthCurrent == HealthMax -> 100%

        AddonBits bits = new();

        HybridDecisionEngine engine = CreateEngine(provider, bits);

        // Act
        bool result = InvokeDetectUnexpectedState(engine);

        // Assert
        result.Should().BeFalse("full health is normal — no LLM intervention needed");
    }

    [Fact]
    public void DetectUnexpectedState_LowHealthInCombat_ReturnsFalse()
    {
        // Arrange — 5% health but IN combat
        FakeAddonDataProvider provider = new();
        provider.Data[10] = 100;
        provider.Data[11] = 5;   // ~5% health
        provider.Data[8] = Mask._14; // bit 14 = Combat flag in v1

        AddonBits bits = new();
        bits.Update(provider); // Sets Combat() == true

        HybridDecisionEngine engine = CreateEngine(provider, bits);

        // Act
        bool result = InvokeDetectUnexpectedState(engine);

        // Assert
        result.Should().BeFalse(
            "low health while actively in combat is expected — GOAP handles this normally");
    }

    [Fact]
    public void DetectUnexpectedState_ExactlyAt20Percent_ReturnsFalse()
    {
        // Arrange — exactly at the 20% threshold (not below it)
        // HealthPercent = (1+20)*100/(1+100) = 2100/101 = 20
        FakeAddonDataProvider provider = new();
        provider.Data[10] = 100;
        provider.Data[11] = 20; // (1+20)*100/(1+100) = 2100/101 ≈ 20

        AddonBits bits = new();

        HybridDecisionEngine engine = CreateEngine(provider, bits);

        // Act
        bool result = InvokeDetectUnexpectedState(engine);

        // Assert
        result.Should().BeFalse("health at exactly 20% does not meet the <20 threshold");
    }

    // --- Helpers ---

    private static HybridDecisionEngine CreateEngine(FakeAddonDataProvider provider, AddonBits bits)
    {
        string root = Path.Combine(Path.GetTempPath(), "WowGrindBotTests.Hybrid", Path.GetRandomFileName());
        Directory.CreateDirectory(root);

        DataConfig dataConfig = new() { Root = root, Exp = "wrath" };
        WorldMapAreaDB worldMapAreaDB = new(dataConfig, NullLogger<WorldMapAreaDB>.Instance);
        AreaDB areaDb = (AreaDB)RuntimeHelpers.GetUninitializedObject(typeof(AreaDB));
        SpellInRange spellInRange = new();
        Stance stance = new();

        PlayerReader playerReader = new(provider, worldMapAreaDB, areaDb, bits, spellInRange, stance);

        return new HybridDecisionEngine(
            NullLogger<HybridDecisionEngine>.Instance,
            goapAgent: null!,
            llmFactory: null!,
            options: Options.Create(new FeatureFlagsOptions()),
            playerReader: playerReader,
            bits: bits);
    }

    private static bool InvokeDetectUnexpectedState(HybridDecisionEngine engine)
    {
        MethodInfo method = typeof(HybridDecisionEngine)
            .GetMethod("DetectUnexpectedState",
                BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (bool)method.Invoke(engine, null)!;
    }

    private sealed class FakeAddonDataProvider : IAddonDataProvider
    {
        public int[] Data { get; } = new int[256];
        public void UpdateData() { }
        public void InitFrames(DataFrame[] frames) { }
        public void Dispose() { }
    }
}
