using System;
using System.Numerics;
using System.Threading;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using SharedLib.Extensions;

using WowheadDB;

using static System.IO.File;
using static System.IO.Path;

namespace Core.Database;

public sealed class AreaDB : IDisposable
{
    private readonly ILogger logger;
    private readonly DataConfig dataConfig;

    private readonly CancellationToken ct;
    private readonly ManualResetEventSlim resetEvent;
    private readonly Thread thread;

    private int areaId = -1;
    public Area? CurrentArea { private set; get; }

    public event Action? Changed;

    public AreaDB(ILogger logger, DataConfig dataConfig,
        CancellationTokenSource cts)
    {
        this.logger = logger;
        this.dataConfig = dataConfig;
        ct = cts.Token;
        resetEvent = new();

        thread = new(ReadArea);
        thread.Start();
    }

    public void Dispose()
    {
        resetEvent.Set();
    }

    public void Update(int areaId)
    {
        if (this.areaId == areaId)
            return;

        this.areaId = areaId;
        resetEvent.Set();
    }

    private void ReadArea()
    {
        resetEvent.Wait();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                CurrentArea = JsonConvert.DeserializeObject<Area>(
                    ReadAllText(Join(dataConfig.ExpArea, $"{areaId}.json")));

                Changed?.Invoke();
            }
            catch (Exception e)
            {
                logger.LogError(e.Message, e.StackTrace);
            }

            resetEvent.Reset();
            resetEvent.Wait();
        }
    }

    public Vector3 GetNearestVendor(Vector3 map)
    {
        if (CurrentArea == null || CurrentArea.vendor.Count == 0)
            return Vector3.Zero;

        NPC closestNpc = CurrentArea.vendor[0];
        float mapDistance = map.MapDistanceXYTo(closestNpc.MapCoords[0]);

        for (int i = 0; i < CurrentArea.vendor.Count; i++)
        {
            NPC npc = CurrentArea.vendor[i];
            float d = map.MapDistanceXYTo(npc.MapCoords[0]);
            if (d < mapDistance)
            {
                mapDistance = d;
                closestNpc = npc;
            }
        }

        return closestNpc.MapCoords[0];
    }
}
