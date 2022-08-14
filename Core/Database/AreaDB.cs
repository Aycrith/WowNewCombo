using System;
using System.Threading;
using System.IO;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SharedLib.Extensions;
using WowheadDB;

namespace Core.Database
{
    public class AreaDB : IDisposable
    {
        private readonly ILogger logger;
        private readonly DataConfig dataConfig;

        private readonly CancellationToken ct;
        private readonly AutoResetEvent autoResetEvent;
        private readonly Thread thread;

        private int areaId = -1;
        public Area? CurrentArea { private set; get; }

        public event Action? Changed;

        public AreaDB(ILogger logger, DataConfig dataConfig, CancellationTokenSource cts)
        {
            this.logger = logger;
            this.dataConfig = dataConfig;
            ct = cts.Token;
            autoResetEvent = new AutoResetEvent(false);

            thread = new(ReadArea);
            thread.Start();
        }

        public void Dispose()
        {
            autoResetEvent.Set();
        }

        public void Update(int areaId)
        {
            if (this.areaId == areaId)
                return;

            this.areaId = areaId;
            autoResetEvent.Set();
        }

        private void ReadArea()
        {
            autoResetEvent.WaitOne();

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    CurrentArea = JsonConvert.DeserializeObject<Area>(File.ReadAllText(Path.Join(dataConfig.ExpArea, $"{areaId}.json")));
                    Changed?.Invoke();
                }
                catch (Exception e)
                {
                    logger.LogError(e.Message, e.StackTrace);
                }

                autoResetEvent.WaitOne();
            }
        }

        public Vector3? GetNearestVendor(Vector3 map)
        {
            if (CurrentArea == null || CurrentArea.vendor.Count == 0)
                return null;

            NPC nearest = CurrentArea.vendor[0];
            float mapDistance = map.MapDistanceXYTo(nearest.points[0]);

            CurrentArea.vendor.ForEach(npc =>
            {
                var mapDist = map.MapDistanceXYTo(npc.points[0]);
                if (mapDist < mapDistance)
                {
                    mapDistance = mapDist;
                    nearest = npc;
                }
            });

            return nearest.points[0];
        }
    }
}
