using System.Text.Json;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using PPather.Data;
using System.Text;
using System.Numerics;
using SharedLib;
using SharedLib.Converters;

namespace Core
{
    public class RemotePathingAPI : IPPather
    {
        private readonly ILogger logger;

        private readonly string host = "localhost";

        private readonly int port = 5001;

        private readonly string api;

        private readonly Stopwatch stopwatch;

        private readonly JsonSerializerOptions options;

        public RemotePathingAPI(ILogger logger, string host = "", int port = 0)
        {
            this.logger = logger;
            this.host = host;
            this.port = port;

            stopwatch = new();

            options = new()
            {
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new Vector3Converter());
            options.Converters.Add(new Vector4Converter());

            api = $"http://{host}:{port}/api/PPather/";
        }

        public async ValueTask DrawLines(List<LineArgs> lineArgs)
        {
            using var client = new HttpClient();
            using var content = new StringContent(JsonSerializer.Serialize(lineArgs, options), Encoding.UTF8, "application/json");
            LogInformation($"Drawing lines '{string.Join(", ", lineArgs.Select(l => l.MapId))}'...");
            await client.PostAsync($"{api}Drawlines", content);
        }

        public async ValueTask DrawSphere(SphereArgs args)
        {
            using var client = new HttpClient();
            using var content = new StringContent(JsonSerializer.Serialize(args, options), Encoding.UTF8, "application/json");
            await client.PostAsync($"{api}DrawSphere", content);
        }

        public async ValueTask<List<Vector3>> FindRoute(int map, Vector3 fromPoint, Vector3 toPoint)
        {
            try
            {
                LogInformation($"Finding route from {fromPoint} map {map} to {toPoint} map {map}...");
                var url = $"{api}MapRoute?uimap1={map}&x1={fromPoint.X}&y1={fromPoint.Y}&uimap2={map}&x2={toPoint.X}&y2={toPoint.Y}";

                stopwatch.Restart();
                using var client = new HttpClient();
                string responseString = await client.GetStringAsync(url);
                LogInformation($"Finding route from {fromPoint} map {map} to {toPoint} took {stopwatch.ElapsedMilliseconds} ms.");
                return JsonSerializer.Deserialize<List<Vector3>>(responseString, options) ?? new();
            }
            catch (Exception ex)
            {
                LogError($"Finding route from {fromPoint} to {toPoint}", ex);
                Console.WriteLine(ex);
                return new();
            }
        }

        public async Task<bool> PingServer()
        {
            try
            {
                var url = $"{api}SelfTest";

                using var client = new HttpClient();
                var responseString = await client.GetStringAsync(url);
                var result = JsonSerializer.Deserialize<bool>(responseString);
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

        #endregion
    }
}