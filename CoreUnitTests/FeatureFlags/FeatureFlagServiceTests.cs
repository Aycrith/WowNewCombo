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

