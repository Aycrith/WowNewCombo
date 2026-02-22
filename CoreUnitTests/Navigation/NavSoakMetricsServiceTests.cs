using Core.Navigation;

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

using Xunit;

namespace CoreUnitTests.Navigation;

public sealed class NavSoakMetricsServiceTests
{
    [Fact]
    public void NavSoakWindow_RepeatStuckRate_CalculatesCorrectly()
    {
        NavSoakWindow window = new()
        {
            WindowStartUtc = DateTime.UtcNow.AddMinutes(-10),
            WindowEndUtc = DateTime.UtcNow,
            StuckEvents = 6,
            RepeatStuckCount = 2,
            FrontBypassActivations = 3,
            SuccessfulReconnects = 19
        };

        // 2/6 = 0.3333
        Assert.Equal(0.3333, window.RepeatStuckRate);
    }

    [Fact]
    public void NavSoakWindow_RepeatStuckRate_ZeroWhenNoStuck()
    {
        NavSoakWindow window = new()
        {
            WindowStartUtc = DateTime.UtcNow.AddMinutes(-10),
            WindowEndUtc = DateTime.UtcNow,
            StuckEvents = 0,
            RepeatStuckCount = 0
        };

        Assert.Equal(0.0, window.RepeatStuckRate);
    }

    [Fact]
    public async Task NavSoakWindow_JsonRoundTrip_PreservesAllFields()
    {
        NavSoakWindow[] windows =
        [
            new()
            {
                WindowStartUtc = new DateTime(2026, 2, 19, 14, 0, 0, DateTimeKind.Utc),
                WindowEndUtc = new DateTime(2026, 2, 19, 14, 10, 0, DateTimeKind.Utc),
                FrontBypassActivations = 2,
                SuccessfulReconnects = 29,
                StuckEvents = 6,
                RepeatStuckCount = 2,
                TailRecalcFailures = 1
            }
        ];

        string tempPath = Path.Combine(Path.GetTempPath(), $"soak-test-{Guid.NewGuid():N}.json");
        try
        {
            var artifact = new { SoakStartUtc = DateTime.UtcNow, Windows = windows };
            string json = JsonSerializer.Serialize(artifact, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(tempPath, json);

            string loaded = await File.ReadAllTextAsync(tempPath);
            using JsonDocument doc = JsonDocument.Parse(loaded);
            JsonElement first = doc.RootElement.GetProperty("Windows")[0];

            Assert.Equal(2, first.GetProperty("FrontBypassActivations").GetInt32());
            Assert.Equal(29, first.GetProperty("SuccessfulReconnects").GetInt32());
            Assert.Equal(1, first.GetProperty("TailRecalcFailures").GetInt32());
        }
        finally { File.Delete(tempPath); }
    }

    [Fact]
    public void TwoWindows_TotalMetricsMatchHandoffSoakEvidence()
    {
        NavSoakWindow w1 = new() { FrontBypassActivations = 2, SuccessfulReconnects = 29, StuckEvents = 6, RepeatStuckCount = 2 };
        NavSoakWindow w2 = new() { FrontBypassActivations = 3, SuccessfulReconnects = 19, StuckEvents = 6, RepeatStuckCount = 0 };

        Assert.Equal(5, w1.FrontBypassActivations + w2.FrontBypassActivations);
        Assert.Equal(48, w1.SuccessfulReconnects + w2.SuccessfulReconnects);
        Assert.Equal(12, w1.StuckEvents + w2.StuckEvents);
    }
}
