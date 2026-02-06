using Core.FeatureFlags;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace CoreUnitTests.FeatureFlags;

public sealed class FeatureFlagServiceTests
{
    [Fact]
    public async Task StartAsync_LoadsRuntimeFeatureFlagsFile()
    {
        string root = Path.Combine(Path.GetTempPath(), "WowClassicGrindBot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        string flagsPath = Path.Combine(root, "runtime_feature_flags.json");

        try
        {
            File.WriteAllText(flagsPath,
                """
                {
                  "Features": {
                    "HazardAvoidance": { "Enabled": true },
                    "ObjectPooling": { "Enabled": false }
                  },
                  "GlobalKillSwitch": false,
                  "DebugMode": true
                }
                """);

            FeatureFlagsOptions defaults = new();
            IOptionsMonitor<FeatureFlagsOptions> monitor = new FixedOptionsMonitor<FeatureFlagsOptions>(defaults);

            FeatureFlagService service = new(
                NullLogger<FeatureFlagService>.Instance,
                monitor,
                Options.Create(new FeatureFlagServiceOptions { ConfigFilePath = flagsPath }));

            await service.StartAsync(CancellationToken.None);

            Assert.True(service.Current.DebugMode);
            Assert.True(service.Current.HazardAvoidance.Enabled);
            Assert.False(service.Current.ObjectPooling.Enabled);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task StartAsync_MissingFile_KeepsDefaults()
    {
        string root = Path.Combine(Path.GetTempPath(), "WowClassicGrindBot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        string flagsPath = Path.Combine(root, "runtime_feature_flags.json");

        try
        {
            FeatureFlagsOptions defaults = new()
            {
                HazardAvoidance = new HazardAvoidanceOptions { Enabled = false },
                Humanization = new HumanizationOptions { Enabled = true }
            };

            IOptionsMonitor<FeatureFlagsOptions> monitor = new FixedOptionsMonitor<FeatureFlagsOptions>(defaults);
            using FeatureFlagService service = new(
                NullLogger<FeatureFlagService>.Instance,
                monitor,
                Options.Create(new FeatureFlagServiceOptions { ConfigFilePath = flagsPath }));

            await service.StartAsync(CancellationToken.None);

            Assert.False(service.Current.HazardAvoidance.Enabled);
            Assert.True(service.Current.Humanization.Enabled);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task HotReload_ModifyingFile_UpdatesCurrent_AndRaisesOnFlagsChanged()
    {
        string root = Path.Combine(Path.GetTempPath(), "WowClassicGrindBot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string flagsPath = Path.Combine(root, "runtime_feature_flags.json");

        try
        {
            File.WriteAllText(flagsPath, """
            {
              "Features": {
                "HazardAvoidance": { "Enabled": false },
                "ObjectPooling": { "Enabled": true }
              },
              "GlobalKillSwitch": false,
              "DebugMode": false
            }
            """);

            FeatureFlagsOptions defaults = new();
            IOptionsMonitor<FeatureFlagsOptions> monitor = new FixedOptionsMonitor<FeatureFlagsOptions>(defaults);

            using FeatureFlagService service = new(
                NullLogger<FeatureFlagService>.Instance,
                monitor,
                Options.Create(new FeatureFlagServiceOptions { ConfigFilePath = flagsPath }));

            int changedCount = 0;
            service.OnFlagsChanged += _ => Interlocked.Increment(ref changedCount);

            await service.StartAsync(CancellationToken.None);
            Assert.False(service.Current.HazardAvoidance.Enabled);

            File.WriteAllText(flagsPath, """
            {
              "Features": {
                "HazardAvoidance": { "Enabled": true },
                "ObjectPooling": { "Enabled": false }
              },
              "GlobalKillSwitch": false,
              "DebugMode": true
            }
            """);

            await WaitUntilAsync(() => service.Current.HazardAvoidance.Enabled, TimeSpan.FromSeconds(5));
            Assert.True(service.Current.DebugMode);
            Assert.False(service.Current.ObjectPooling.Enabled);
            Assert.True(Volatile.Read(ref changedCount) >= 1);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task HotReload_MalformedJson_DoesNotCrash_AndKeepsLastValidState()
    {
        string root = Path.Combine(Path.GetTempPath(), "WowClassicGrindBot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string flagsPath = Path.Combine(root, "runtime_feature_flags.json");

        try
        {
            File.WriteAllText(flagsPath, """
            {
              "Features": {
                "HazardAvoidance": { "Enabled": true }
              },
              "GlobalKillSwitch": false,
              "DebugMode": false
            }
            """);

            IOptionsMonitor<FeatureFlagsOptions> monitor = new FixedOptionsMonitor<FeatureFlagsOptions>(new FeatureFlagsOptions());
            using FeatureFlagService service = new(
                NullLogger<FeatureFlagService>.Instance,
                monitor,
                Options.Create(new FeatureFlagServiceOptions { ConfigFilePath = flagsPath }));

            await service.StartAsync(CancellationToken.None);
            await WaitUntilAsync(() => service.Current.HazardAvoidance.Enabled, TimeSpan.FromSeconds(5));

            File.WriteAllText(flagsPath, "{ \"Features\": { \"HazardAvoidance\": ");

            await Task.Delay(350);
            Assert.True(service.Current.HazardAvoidance.Enabled);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task Dispose_AfterWatcherActivity_DoesNotThrow()
    {
        string root = Path.Combine(Path.GetTempPath(), "WowClassicGrindBot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string flagsPath = Path.Combine(root, "runtime_feature_flags.json");

        try
        {
            File.WriteAllText(flagsPath, """
            {
              "Features": {
                "HazardAvoidance": { "Enabled": false }
              },
              "GlobalKillSwitch": false,
              "DebugMode": false
            }
            """);

            IOptionsMonitor<FeatureFlagsOptions> monitor = new FixedOptionsMonitor<FeatureFlagsOptions>(new FeatureFlagsOptions());
            FeatureFlagService service = new(
                NullLogger<FeatureFlagService>.Instance,
                monitor,
                Options.Create(new FeatureFlagServiceOptions { ConfigFilePath = flagsPath }));

            await service.StartAsync(CancellationToken.None);

            File.WriteAllText(flagsPath, """
            {
              "Features": {
                "HazardAvoidance": { "Enabled": true }
              },
              "GlobalKillSwitch": false,
              "DebugMode": false
            }
            """);

            await WaitUntilAsync(() => service.Current.HazardAvoidance.Enabled, TimeSpan.FromSeconds(5));

            Exception? disposeException = Record.Exception(service.Dispose);
            Assert.Null(disposeException);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.True(condition(), "Timed out waiting for condition.");
    }

    private sealed class FixedOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        private readonly T value = value;

        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable OnChange(Action<T, string?> listener) => new NoopDisposable();

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
