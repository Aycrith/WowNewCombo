using System;
using System.IO;
using System.Numerics;
using System.Threading;

using Core.FeatureFlags;
using Core.GoalsComponent.Blacklist;

using Microsoft.Extensions.Logging;

using Xunit;

namespace CoreUnitTests.GoalsComponent.Blacklist;

public sealed class SmartBlacklistTests : IDisposable
{
    private readonly string _testDir;
    private readonly SmartBlacklistOptions _options;
    private readonly SmartBlacklist _blacklist;

    public SmartBlacklistTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"SmartBlacklistTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);

        _options = new SmartBlacklistOptions
        {
            MaxEntries = 100,
            AutoSaveIntervalMinutes = 0, // Disable auto-save for tests
            AutoSaveOnChange = false,
            LogBlacklistHits = false
        };

        // Override persistence path via reflection for testing
        _blacklist = CreateTestBlacklist();
    }

    private SmartBlacklist CreateTestBlacklist()
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<SmartBlacklist>.Instance;
        var blacklist = new SmartBlacklist(logger, _options, Path.Combine(_testDir, "test_blacklist.json"));
        return blacklist;
    }

    public void Dispose()
    {
        _blacklist.Dispose();
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }

    [Fact]
    public void Is_EmptyBlacklist_ReturnsFalse()
    {
        Assert.False(_blacklist.Is(12345));
    }

    [Fact]
    public void Is_GuidZero_ReturnsFalse()
    {
        Assert.False(_blacklist.Is(0));
    }

    [Theory]
    [InlineData(BlacklistSeverity.Temporary)]
    [InlineData(BlacklistSeverity.Medium)]
    [InlineData(BlacklistSeverity.Permanent)]
    public void Add_AllSeverities_BlacklistReturnsTrue(BlacklistSeverity severity)
    {
        int guid = 12345;
        string name = "TestMob";
        string reason = "Test reason";
        Vector3 position = new(100, 200, 300);
        int mapId = 1;

        _blacklist.Add(guid, name, severity, reason, position, mapId);

        Assert.True(_blacklist.Is(guid));
    }

    [Fact]
    public void Add_TemporarySeverity_ExpiresAfterTtl()
    {
        int guid = 12345;
        var customTtl = TimeSpan.FromMilliseconds(100);

        _blacklist.Add(guid, "TestMob", BlacklistSeverity.Temporary, "Test", Vector3.Zero, 1, customTtl);

        Assert.True(_blacklist.Is(guid));

        Thread.Sleep(150);

        Assert.False(_blacklist.Is(guid));
    }

    [Fact]
    public void Add_PermanentSeverity_NeverExpires()
    {
        int guid = 12345;

        _blacklist.Add(guid, "TestMob", BlacklistSeverity.Permanent, "Test", Vector3.Zero, 1);

        Assert.True(_blacklist.Is(guid));
        Assert.Null(_blacklist.GetEntries().Find(e => e.TargetGuid == guid).ExpiresAt);
    }

    [Fact]
    public void Add_DuplicateEntry_IncrementsHitCount()
    {
        int guid = 12345;

        _blacklist.Add(guid, "TestMob", BlacklistSeverity.Temporary, "Test", Vector3.Zero, 1);
        _blacklist.Add(guid, "TestMob", BlacklistSeverity.Temporary, "Test", Vector3.Zero, 1);

        var entry = _blacklist.GetEntries().Find(e => e.TargetGuid == guid);
        Assert.Equal(2, entry.HitCount);
    }

    [Fact]
    public void Add_UpgradesSeverity_WhenHigherProvided()
    {
        int guid = 12345;

        _blacklist.Add(guid, "TestMob", BlacklistSeverity.Temporary, "Test", Vector3.Zero, 1);
        _blacklist.Add(guid, "TestMob", BlacklistSeverity.Medium, "Upgraded", Vector3.Zero, 1);

        var entry = _blacklist.GetEntries().Find(e => e.TargetGuid == guid);
        Assert.Equal(BlacklistSeverity.Medium, entry.Severity);
    }

    [Fact]
    public void Remove_ExistingEntry_ReturnsTrue()
    {
        int guid = 12345;
        _blacklist.Add(guid, "TestMob", BlacklistSeverity.Temporary, "Test", Vector3.Zero, 1);

        Assert.True(_blacklist.Remove(guid));
        Assert.False(_blacklist.Is(guid));
    }

    [Fact]
    public void Remove_NonExistentEntry_ReturnsFalse()
    {
        Assert.False(_blacklist.Remove(99999));
    }

    [Fact]
    public void PruneExpired_RemovesOnlyExpired()
    {
        int expiredGuid = 1;
        int validGuid = 2;

        _blacklist.Add(expiredGuid, "Expired", BlacklistSeverity.Temporary, "Test", Vector3.Zero, 1, TimeSpan.FromMilliseconds(1));
        _blacklist.Add(validGuid, "Valid", BlacklistSeverity.Permanent, "Test", Vector3.Zero, 1);

        Thread.Sleep(10);
        int pruned = _blacklist.PruneExpired();

        Assert.Equal(1, pruned);
        Assert.False(_blacklist.Is(expiredGuid));
        Assert.True(_blacklist.Is(validGuid));
    }

    [Fact]
    public void GetEntries_WithMinSeverity_FiltersCorrectly()
    {
        _blacklist.Add(1, "Temp", BlacklistSeverity.Temporary, "Test", Vector3.Zero, 1);
        _blacklist.Add(2, "Medium", BlacklistSeverity.Medium, "Test", Vector3.Zero, 1);
        _blacklist.Add(3, "Permanent", BlacklistSeverity.Permanent, "Test", Vector3.Zero, 1);

        var mediumAndAbove = _blacklist.GetEntries(BlacklistSeverity.Medium);

        Assert.Equal(2, mediumAndAbove.Count);
        Assert.DoesNotContain(mediumAndAbove, e => e.Severity == BlacklistSeverity.Temporary);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        _blacklist.Add(1, "Test1", BlacklistSeverity.Temporary, "Test", Vector3.Zero, 1);
        _blacklist.Add(2, "Test2", BlacklistSeverity.Temporary, "Test", Vector3.Zero, 1);

        _blacklist.Clear();

        Assert.Equal(0, _blacklist.Count);
        Assert.False(_blacklist.Is(1));
        Assert.False(_blacklist.Is(2));
    }

    [Fact]
    public void SaveToDisk_CreatesFile()
    {
        int guid = 12345;
        _blacklist.Add(guid, "TestMob", BlacklistSeverity.Temporary, "Test", Vector3.Zero, 1);

        _blacklist.SaveToDisk();

        string path = Path.Combine(_testDir, "test_blacklist.json");
        Assert.True(File.Exists(path));

        string content = File.ReadAllText(path);
        Assert.Contains("TestMob", content);
        Assert.Contains(guid.ToString(), content);
    }

    [Fact]
    public void LoadFromDisk_RestoresEntries()
    {
        int guid = 12345;
        string name = "TestMob";

        // Create and save initial blacklist
        _blacklist.Add(guid, name, BlacklistSeverity.Permanent, "Test", Vector3.Zero, 1);
        _blacklist.SaveToDisk();

        // Create new blacklist instance (simulates restart)
        var newBlacklist = CreateTestBlacklist();

        Assert.True(newBlacklist.Is(guid));
        var entry = newBlacklist.GetEntries().Find(e => e.TargetGuid == guid);
        Assert.Equal(name, entry.TargetName);

        newBlacklist.Dispose();
    }

    [Fact]
    public void Count_ReturnsCorrectNumber()
    {
        Assert.Equal(0, _blacklist.Count);

        _blacklist.Add(1, "Test1", BlacklistSeverity.Temporary, "Test", Vector3.Zero, 1);
        Assert.Equal(1, _blacklist.Count);

        _blacklist.Add(2, "Test2", BlacklistSeverity.Temporary, "Test", Vector3.Zero, 1);
        Assert.Equal(2, _blacklist.Count);
    }

    [Fact]
    public void TotalHits_TracksAccesses()
    {
        int guid = 12345;
        _blacklist.Add(guid, "TestMob", BlacklistSeverity.Temporary, "Test", Vector3.Zero, 1);

        _blacklist.Is(guid);
        _blacklist.Is(guid);
        _blacklist.Is(guid);

        Assert.Equal(3, _blacklist.TotalHits);
    }

    [Fact]
    public void GetEntries_OrdersByLastAccessed()
    {
        _blacklist.Add(1, "First", BlacklistSeverity.Permanent, "Test", Vector3.Zero, 1);
        Thread.Sleep(10);
        _blacklist.Add(2, "Second", BlacklistSeverity.Permanent, "Test", Vector3.Zero, 1);
        Thread.Sleep(10);
        _blacklist.Add(3, "Third", BlacklistSeverity.Permanent, "Test", Vector3.Zero, 1);

        // Access first entry to update its LastAccessedAt
        _blacklist.Is(1);

        var entries = _blacklist.GetEntries();
        Assert.Equal(1, entries[0].TargetGuid); // Most recently accessed
    }
}
