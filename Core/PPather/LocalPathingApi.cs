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
using System;

#pragma warning disable 162

namespace Core
{
    public class LocalPathingApi : IPPather
    {
        private const bool debug = false;

        private readonly ILogger logger;

        private readonly PPatherService service;

        private readonly bool Enabled;

        private readonly Stopwatch stopwatch;

        private DateTime lastSave;

        public LocalPathingApi(ILogger logger, PPatherService service, DataConfig dataConfig)
        {
            this.logger = logger;
            this.service = service;
            stopwatch = new();

            var mpqFiles = MPQTriangleSupplier.GetArchiveNames(dataConfig);
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

        public ValueTask<List<Vector3>> FindRoute(int uimap, Vector3 fromPoint, Vector3 toPoint)
        {
            if (!Enabled)
            {
                LogWarning($"Pathing is disabled, please check the messages when the bot started.");
                return new ValueTask<List<Vector3>>();
            }

            stopwatch.Restart();

            service.SetLocations(service.ToWorld(uimap, fromPoint.X, fromPoint.Y, fromPoint.Z), service.ToWorld(uimap, toPoint.X, toPoint.Y));
            var path = service.DoSearch(PPather.Graph.PathGraph.eSearchScoreSpot.A_Star_With_Model_Avoidance);

            stopwatch.Stop();

            if (path == null)
            {
                if (debug)
                    LogWarning($"Failed to find a path from {fromPoint} to {toPoint}");

                return new ValueTask<List<Vector3>>();
            }
            else
            {
                if (debug)
                    LogDebug($"Finding route from {fromPoint} map {uimap} to {toPoint} took {stopwatch.ElapsedMilliseconds} ms.");

                if ((DateTime.UtcNow - lastSave).TotalMinutes >= 1)
                {
                    service.Save();
                    lastSave = DateTime.UtcNow;
                }
            }

            return
                new ValueTask<List<Vector3>>(path.locations
                    .Select(s => service.ToLocal(s, (int)service.SearchFrom!.Value.W, uimap)).ToList());
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