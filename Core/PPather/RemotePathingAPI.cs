using Newtonsoft.Json;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Core.PPather;
using System.Text;
using System.Numerics;

namespace Core
{
    public class RemotePathingAPI : IPPather
    {
        private readonly ILogger logger;

        private string host = "localhost";

        private int port = 5001;

        private string api => $"http://{host}:{port}/api/PPather/";

        private List<LineArgs> lineArgs = new List<LineArgs>();

        public RemotePathingAPI(ILogger logger, string host = "", int port = 0)
        {
            this.logger = logger;
            this.host = host;
            this.port = port;
        }

        public async ValueTask DrawLines(List<LineArgs> lineArgs)
        {
            this.lineArgs = lineArgs;

            using var client = new HttpClient();
            using var content = new StringContent(JsonConvert.SerializeObject(lineArgs), Encoding.UTF8, "application/json");
            LogInformation($"Drawing lines '{string.Join(", ", lineArgs.Select(l => l.MapId))}'...");
            await client.PostAsync($"{api}Drawlines", content);
        }

        public async ValueTask DrawLines()
        {
            await DrawLines(lineArgs);
        }

        public async ValueTask DrawSphere(SphereArgs args)
        {
            using var client = new HttpClient();
            using var content = new StringContent(JsonConvert.SerializeObject(args), Encoding.UTF8, "application/json");
            await client.PostAsync($"{api}DrawSphere", content);
        }

        public async ValueTask<List<Vector3>> FindRoute(int map, Vector3 fromPoint, Vector3 toPoint)
        {
            try
            {
                LogInformation($"Finding route from {fromPoint} map {map} to {toPoint} map {map}...");
                var url = $"{api}MapRoute?map1={map}&x1={fromPoint.X}&y1={fromPoint.Y}&map2={map}&x2={toPoint.X}&y2={toPoint.Y}";
                var sw = new Stopwatch();
                sw.Start();

                using var client = new HttpClient();
                var responseString = await client.GetStringAsync(url);
                LogInformation($"Finding route from {fromPoint} map {map} to {toPoint} took {sw.ElapsedMilliseconds} ms.");
                var path = JsonConvert.DeserializeObject<IEnumerable<WorldMapAreaSpot>>(responseString);
                var result = path.Select(l => new Vector3(l.X, l.Y, l.Z)).ToList();
                return result;
            }
            catch (Exception ex)
            {
                LogError($"Finding route from {fromPoint} to {toPoint}", ex);
                Console.WriteLine(ex);
                return new List<Vector3>();
            }
        }

        public async Task<bool> PingServer()
        {
            try
            {
                var url = $"{api}SelfTest";

                using var client = new HttpClient();
                var responseString = await client.GetStringAsync(url);
                var result = JsonConvert.DeserializeObject<bool>(responseString);
                return result;
            }
            catch (Exception ex)
            {
                LogError($"{nameof(PingServer)} - Connected: false - {api} Gave({ex.Message})");
                return false;
            }
        }

        #region Logging

        private void LogError(string text, Exception? ex = null)
        {
            logger.LogError(ex, $"{nameof(RemotePathingAPI)}: {text}");
        }

        private void LogInformation(string text)
        {
            logger.LogInformation($"{nameof(RemotePathingAPI)}: {text}");
        }

        private void LogDebug(string text)
        {
            logger.LogDebug($"{nameof(RemotePathingAPI)}: {text}");
        }

        private void LogWarning(string text)
        {
            logger.LogWarning($"{nameof(RemotePathingAPI)}: {text}");
        }

        #endregion
    }
}