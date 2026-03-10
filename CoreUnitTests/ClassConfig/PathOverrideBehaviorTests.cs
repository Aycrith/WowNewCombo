using Core;

using Xunit;

namespace CoreUnitTests.ClassConfig;

public sealed class PathOverrideBehaviorTests
{
    [Fact]
    public void ApplyPathOverride_DoesNotMutateDefaultPathFilename()
    {
        PathSettings settings = new()
        {
            PathFilename = "..\\json\\path\\_pack\\1-20\\Blood elf\\15-20_Ghostlands_Windrunner.json",
            OverridePathFilename = string.Empty
        };

        ClassConfiguration.ApplyPathOverride(settings, "_pack\\1-20\\Blood elf\\18-22_Ghostlands_Deatholme_Approach.json");

        Assert.Equal("..\\json\\path\\_pack\\1-20\\Blood elf\\15-20_Ghostlands_Windrunner.json", settings.PathFilename);
        Assert.Equal("_pack\\1-20\\Blood elf\\18-22_Ghostlands_Deatholme_Approach.json", settings.OverridePathFilename);
        Assert.Equal("_pack\\1-20\\Blood elf\\18-22_Ghostlands_Deatholme_Approach.json", ClassConfiguration.GetEffectivePathFilename(settings));
    }

    [Fact]
    public void ApplyPathOverride_WhenCleared_RestoresEffectiveDefaultPath()
    {
        PathSettings settings = new()
        {
            PathFilename = "..\\json\\path\\_pack\\1-20\\Blood elf\\15-20_Ghostlands_Windrunner.json",
            OverridePathFilename = "_pack\\1-20\\Blood elf\\18-22_Ghostlands_Deatholme_Approach.json"
        };

        ClassConfiguration.ApplyPathOverride(settings, "   ");

        Assert.Equal(string.Empty, settings.OverridePathFilename);
        Assert.Equal("..\\json\\path\\_pack\\1-20\\Blood elf\\15-20_Ghostlands_Windrunner.json", ClassConfiguration.GetEffectivePathFilename(settings));
    }
}
