using Core;

using Newtonsoft.Json;

using System.IO;
using System.Linq;

using Xunit;

namespace CoreUnitTests.CombatRotation;

public sealed class WarlockProfileBehaviorTests
{
    private static readonly string ProfilePath =
        Path.Combine(FindRepoRoot(), "Json", "class", "BloodElf_Warlock_1-70_TBC.json");

    [Fact]
    public void BloodElfWarlockProfile_PullStartsWithCurseOfAgony()
    {
        ClassConfiguration? config = LoadProfileOrSkip();
        if (config == null)
        {
            return;
        }

        Assert.NotEmpty(config.Pull.Sequence);
        Assert.Equal("Curse of Agony", config.Pull.Sequence[0].Name);
    }

    [Fact]
    public void BloodElfWarlockProfile_CombatIncludesLowHpFear()
    {
        ClassConfiguration? config = LoadProfileOrSkip();
        if (config == null)
        {
            return;
        }

        KeyAction? fear = config.Combat.Sequence.FirstOrDefault(static a => a.Name == "Fear");
        Assert.NotNull(fear);
        Assert.Contains("Health% < FEAR_HP", fear.Requirement);
    }

    [Fact]
    public void BloodElfWarlockProfile_AdhocUsesHealthstoneWithPriorityAndKnownTbcIds()
    {
        ClassConfiguration? config = LoadProfileOrSkip();
        if (config == null)
        {
            return;
        }

        KeyAction? useHealthstone = config.Adhoc.Sequence.FirstOrDefault(static a => a.Name == "Use Healthstone");
        Assert.NotNull(useHealthstone);
        Assert.Equal(1f, useHealthstone.Cost);
        Assert.Equal("true", useHealthstone.InCombat);
        Assert.Contains("BagItem:22105", useHealthstone.Requirement);
        Assert.Contains("BagItem:22104", useHealthstone.Requirement);
        Assert.Contains("BagItem:22103", useHealthstone.Requirement);
    }

    [Fact]
    public void BloodElfWarlockProfile_AdhocCreatesHealthstoneWhenNonePresent()
    {
        ClassConfiguration? config = LoadProfileOrSkip();
        if (config == null)
        {
            return;
        }

        KeyAction? createHealthstone = config.Adhoc.Sequence.FirstOrDefault(static a => a.Name == "Create Healthstone");
        Assert.NotNull(createHealthstone);
        Assert.Equal("false", createHealthstone.InCombat);
        Assert.Contains("!BagItem:22105", createHealthstone.Requirement);
        Assert.Contains("!BagItem:22104", createHealthstone.Requirement);
        Assert.Contains("!BagItem:22103", createHealthstone.Requirement);
    }

    private static ClassConfiguration? LoadProfileOrSkip()
    {
        if (!File.Exists(ProfilePath))
        {
            return null;
        }

        string json = File.ReadAllText(ProfilePath);
        return JsonConvert.DeserializeObject<ClassConfiguration>(json);
    }

    private static string FindRepoRoot()
    {
        string? dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "MasterOfPuppets.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        return Path.Combine("..", "..", "..", "..");
    }
}
