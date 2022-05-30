using Core;
using PPather.Data;
using Microsoft.Extensions.Logging;
using PPather;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using WowTriangles;

namespace Core
{
    public class LocalPathingApi : IPPather
    {
        private readonly ILogger logger;

        private readonly PPatherService service;

        private readonly bool Enabled;

        private readonly Stopwatch stopwatch;

        public LocalPathingApi(ILogger logger, PPatherService service)
        {
            this.logger = logger;
            this.service = service;
            stopwatch = new();

            var mpqFiles = MPQTriangleSupplier.GetArchiveNames(DataConfig.Load());
            int countOfMPQFiles = mpqFiles.Count(f => File.Exists(f));
            if (countOfMPQFiles == 0)
            {
                LogWarning("Some of these MPQ files should exist!");
                mpqFiles.ToList().ForEach(LogInformation);
                LogError("No MPQ files found, refer to the Readme to download them.");
                Enabled = false;
            }
            else
            {
                LogDebug("Hooray, MPQ files exist.");
            }

            Enabled = countOfMPQFiles > 0;
        }

        public ValueTask DrawLines(List<LineArgs> lineArgs)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask DrawSphere(SphereArgs args)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<List<Vector3>> FindRoute(int map, Vector3 fromPoint, Vector3 toPoint)
        {
            if (!Enabled)
            {
                LogWarning($"Pathing is disabled, please check the messages when the bot started.");
                return new ValueTask<List<Vector3>>();
            }

            stopwatch.Restart();

            service.SetLocations(service.GetWorldLocation(map, fromPoint.X, fromPoint.Y, fromPoint.Z), service.GetWorldLocation(map, toPoint.X, toPoint.Y));
            var path = service.DoSearch(PPather.Graph.PathGraph.eSearchScoreSpot.A_Star_With_Model_Avoidance);

            stopwatch.Stop();

            if (path == null)
            {
                LogWarning($"Failed to find a path from {fromPoint} to {toPoint}");
                return new ValueTask<List<Vector3>>();
            }
            else
            {
                LogInformation($"Finding route from {fromPoint} map {map} to {toPoint} took {stopwatch.ElapsedMilliseconds} ms.");
                service.Save();
            }

            var worldLocations = path.locations.Select(s => service.ToMapAreaSpot(s.X, s.Y, s.Z, map));
            var result = worldLocations.Select(l => new Vector3(l.X, l.Y, l.Z)).ToList();
            return new ValueTask<List<Vector3>>(result);
        }

        #region Logging

        private void LogError(string text)
        {
            logger.LogError($"{nameof(LocalPathingApi)}: {text}");
        }

        private void LogInformation(string text)
        {
            logger.LogInformation($"{nameof(LocalPathingApi)}: {text}");
        }

        private void LogDebug(string text)
        {
            logger.LogDebug($"{nameof(LocalPathingApi)}: {text}");
        }

        private void LogWarning(string text)
        {
            logger.LogWarning($"{nameof(LocalPathingApi)}: {text}");
        }

        #endregion
    }
}