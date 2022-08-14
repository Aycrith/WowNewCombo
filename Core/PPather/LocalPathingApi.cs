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

        public Vector3[] FindMapRoute(int uiMap, Vector3 mapFrom, Vector3 mapTo)
        {
            if (!Enabled)
            {
                LogWarning($"Pathing is disabled, please check the messages when the bot started.");
                return Array.Empty<Vector3>();
            }

            stopwatch.Restart();

            service.SetLocations(service.ToWorld(uiMap, mapFrom.X, mapFrom.Y, mapFrom.Z), service.ToWorld(uiMap, mapTo.X, mapTo.Y));
            var path = service.DoSearch(PPather.Graph.PathGraph.eSearchScoreSpot.A_Star_With_Model_Avoidance);
            if (path == null)
            {
                if (debug)
                    LogWarning($"Failed to find a path from {mapFrom} to {mapTo}");

                return Array.Empty<Vector3>();
            }
            else
            {
                if (debug)
                    LogDebug($"Finding route from {mapFrom} map {uiMap} to {mapTo} took {stopwatch.ElapsedMilliseconds} ms.");

                if ((DateTime.UtcNow - lastSave).TotalMinutes >= 1)
                {
                    service.Save();
                    lastSave = DateTime.UtcNow;
                }
            }

            Vector3[] array = new Vector3[path.locations.Count];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = service.ToLocal(path.locations[i], (int)service.SearchFrom!.Value.W, uiMap);
            }
            return array;
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