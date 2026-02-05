using Core.Hazard;

using Microsoft.Extensions.Logging.Abstractions;

using System;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace CoreUnitTests.Hazard;

public sealed class LocalHazardDaoTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsEvents()
    {
        string root = Path.Combine(Path.GetTempPath(), "WowClassicGrindBot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            DataConfig config = new()
            {
                Root = root,
                Exp = "tbc"
            };

            LocalHazardDAO dao = new(config, NullLogger<LocalHazardDAO>.Instance);

            HazardEvent[] events =
            [
                new HazardEvent
                {
                    WorldPosition = new Vector3(1, 2, 3),
                    MapX = 12.5f,
                    MapY = 9.25f,
                    MapId = 0,
                    UIMapId = 1519,
                    Type = HazardEventType.Death,
                    Zone = "Stormwind"
                }
            ];

            await dao.SaveAsync("tbc", mapId: 0, events, CancellationToken.None);

            var loaded = await dao.LoadAsync("tbc", mapId: 0, CancellationToken.None);

            Assert.Single(loaded);
            Assert.Equal(events[0].WorldPosition, loaded[0].WorldPosition);
            Assert.Equal(events[0].Type, loaded[0].Type);
            Assert.Equal(events[0].MapId, loaded[0].MapId);
            Assert.Equal(events[0].UIMapId, loaded[0].UIMapId);
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
    public async Task Load_MissingFile_ReturnsEmpty()
    {
        string root = Path.Combine(Path.GetTempPath(), "WowClassicGrindBot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            DataConfig config = new()
            {
                Root = root,
                Exp = "tbc"
            };

            LocalHazardDAO dao = new(config, NullLogger<LocalHazardDAO>.Instance);

            var loaded = await dao.LoadAsync("tbc", mapId: 123, CancellationToken.None);

            Assert.Empty(loaded);
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
}
