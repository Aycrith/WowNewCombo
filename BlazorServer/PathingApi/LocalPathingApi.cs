using Core;
using Core.PPather;
using Microsoft.Extensions.Logging;
using PathingAPI;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using WowTriangles;

namespace BlazorServer
{
    public class LocalPathingApi : IPPather
    {
        private readonly ILogger logger;

        private PPatherService service;

        private bool Enabled = true;

        public LocalPathingApi(ILogger logger, PPatherService service)
        {
            this.logger = logger;
            this.service = service;
        }

        public ValueTask DrawLines(List<LineArgs> lineArgs)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask DrawLines()
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

            var sw = new Stopwatch();
            sw.Start();

            service.SetLocations(service.GetWorldLocation(map, fromPoint.X, fromPoint.Y, fromPoint.Z), service.GetWorldLocation(map, toPoint.X, toPoint.Y));
            var path = service.DoSearch(PatherPath.Graph.PathGraph.eSearchScoreSpot.A_Star_With_Model_Avoidance);

            if (path == null)
            {
                LogWarning($"Failed to find a path from {fromPoint} to {toPoint}");
                return new ValueTask<List<Vector3>>();
            }
            else
            {
                LogInformation($"Finding route from {fromPoint} map {map} to {toPoint} took {sw.ElapsedMilliseconds} ms.");
                service.Save();
            }

            var worldLocations = path.locations.Select(s => service.ToMapAreaSpot(s.X, s.Y, s.Z, map));
            var result = worldLocations.Select(l => new Vector3(l.X, l.Y, l.Z)).ToList();
            return new ValueTask<List<Vector3>>(result);
        }

        public bool SelfTest()
        {
            var mpqFiles = MPQTriangleSupplier.GetArchiveNames(DataConfig.Load(), s => LogInformation(s));

            var countOfMPQFiles = mpqFiles.Where(f => File.Exists(f)).Count();
            if (countOfMPQFiles == 0)
            {
                LogWarning("Some of these MPQ files should exist!");
                mpqFiles.ToList().ForEach(l => LogInformation(l));
                LogError("No MPQ files found, refer to the Readme to download them.");
                Enabled = false;
            }
            else
            {
                LogDebug("Hooray, MPQ files exist.");
            }

            return countOfMPQFiles > 0;
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