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

        public async ValueTask<Vector3[]> FindRoute(int uiMap, Vector3 mapFrom, Vector3 mapTo)
        {
            try
            {
                LogInformation($"Finding route from {mapFrom} map {uiMap} to {mapTo} map {uiMap}...");
                var url = $"{api}MapRoute?uimap1={uiMap}&x1={mapFrom.X}&y1={mapFrom.Y}&uimap2={uiMap}&x2={mapTo.X}&y2={mapTo.Y}";

                stopwatch.Restart();
                using var client = new HttpClient();
                string responseString = await client.GetStringAsync(url);
                LogInformation($"Finding route from {mapFrom} map {uiMap} to {mapTo} took {stopwatch.ElapsedMilliseconds} ms.");
                return JsonSerializer.Deserialize<Vector3[]>(responseString, options) ?? Array.Empty<Vector3>();
            }
            catch (Exception ex)
            {
                LogError($"Finding route from {mapFrom} to {mapTo}", ex);
                Console.WriteLine(ex);
                return Array.Empty<Vector3>();
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