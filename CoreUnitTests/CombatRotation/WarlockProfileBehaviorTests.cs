using Core;

using Newtonsoft.Json;

using System;
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
    public void BloodElfWarlockProfile_CombatUsesDrainLifeBeforeFallbackDamage()
    {
        ClassConfiguration? config = LoadProfileOrSkip();
        if (config == null)
        {
            return;
        }

        KeyAction? drainLife = config.Combat.Sequence.FirstOrDefault(static a => a.Name == "Drain Life");
        KeyAction? shoot = config.Combat.Sequence.FirstOrDefault(static a => a.Name == "Shoot");
        KeyAction? shadowBolt = config.Combat.Sequence.FirstOrDefault(static a => a.Name == "Shadow Bolt");
        Assert.NotNull(drainLife);
        Assert.NotNull(shoot);
        Assert.NotNull(shadowBolt);
        Assert.Contains("Health% < DRAIN_LIFE_HP", drainLife.Requirement);
        Assert.True(Array.IndexOf(config.Combat.Sequence, drainLife) < Array.IndexOf(config.Combat.Sequence, shoot));
        Assert.True(Array.IndexOf(config.Combat.Sequence, drainLife) < Array.IndexOf(config.Combat.Sequence, shadowBolt));
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
        Assert.Equal(1f, createHealthstone.Cost);
        Assert.Contains("BagItem:6265:1", createHealthstone.Requirement);
        Assert.Contains("!BagItem:22105", createHealthstone.Requirement);
        Assert.Contains("!BagItem:22104", createHealthstone.Requirement);
        Assert.Contains("!BagItem:22103", createHealthstone.Requirement);
    }

    [Fact]
    public void BloodElfWarlockProfile_PullRequiresPetAndRecoveryForNormalPulls()
    {
        ClassConfiguration? config = LoadProfileOrSkip();
        if (config == null)
        {
            return;
        }

        KeyAction? curseOfAgony = config.Pull.Sequence.FirstOrDefault(static a => a.Name == "Curse of Agony");
        Assert.NotNull(curseOfAgony);
        Assert.Contains("(Level < 10 || (Has Pet && PetHealth% > 0))", curseOfAgony.Requirements);
        Assert.Contains("Health% >= FOOD_HP", curseOfAgony.Requirements);
    }

    [Fact]
    public void BloodElfWarlockProfile_AdhocPrioritizesPetSummonAndFoodRecovery()
    {
        ClassConfiguration? config = LoadProfileOrSkip();
        if (config == null)
        {
            return;
        }

        KeyAction? summonVoidwalker = config.Adhoc.Sequence.FirstOrDefault(static a => a.Name == "Summon Voidwalker");
        KeyAction? food = config.Parallel.Sequence.FirstOrDefault(static a => a.Name == "Food");
        KeyAction? drink = config.Parallel.Sequence.FirstOrDefault(static a => a.Name == "Drink");
        Assert.NotNull(summonVoidwalker);
        Assert.NotNull(food);
        Assert.NotNull(drink);
        Assert.Equal(1f, summonVoidwalker.Cost);
        Assert.Contains("PetHealth% == 0", summonVoidwalker.Requirement);
        Assert.Contains("BagItem:6265:1", summonVoidwalker.Requirement);
        Assert.Contains("Health% >= FOOD_HP", summonVoidwalker.Requirement);
        Assert.Equal("Mana% < DRINK_MANA", drink.Requirement);
    }

    [Fact]
    public void BloodElfWarlockProfile_AutoAttackRequiresTrueMeleeFallback()
    {
        ClassConfiguration? config = LoadProfileOrSkip();
        if (config == null)
        {
            return;
        }

        KeyAction? autoAttack = config.Combat.Sequence.FirstOrDefault(static a => a.Name == "AutoAttack");
        Assert.NotNull(autoAttack);
        Assert.Contains("InMeleeRange", autoAttack.Requirements);
    }

    [Fact]
    public void BloodElfWarlockProfile_UsesDrainSoulAndWandBeforeShadowBoltSpam()
    {
        ClassConfiguration? config = LoadProfileOrSkip();
        if (config == null)
        {
            return;
        }

        KeyAction? drainSoul = config.Combat.Sequence.FirstOrDefault(static a => a.Name == "Drain Soul");
        KeyAction? shadowBolt = config.Combat.Sequence.FirstOrDefault(static a => a.Name == "Shadow Bolt");
        KeyAction? shoot = config.Combat.Sequence.FirstOrDefault(static a => a.Name == "Shoot");
        Assert.NotNull(drainSoul);
        Assert.NotNull(shadowBolt);
        Assert.NotNull(shoot);
        Assert.Contains("BagItem:6265:0", drainSoul.Requirements);
        Assert.Contains("!HasRangedWeapon", shadowBolt.Requirements);
        Assert.True(Array.IndexOf(config.Combat.Sequence, shoot) < Array.IndexOf(config.Combat.Sequence, shadowBolt));
    }

    [Fact]
    public void BloodElfWarlockProfile_LifeTapSupportsOutOfCombatManaRecovery()
    {
        ClassConfiguration? config = LoadProfileOrSkip();
        if (config == null)
        {
            return;
        }

        KeyAction? lifeTap = config.Adhoc.Sequence.FirstOrDefault(static a => a.Name == "Life Tap");
        Assert.NotNull(lifeTap);
        Assert.Equal(1f, lifeTap.Cost);
        Assert.Contains("Mana% < LIFETAP_MANA", lifeTap.Requirement);
        Assert.Contains("Health% > LIFETAP_HP", lifeTap.Requirement);
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
