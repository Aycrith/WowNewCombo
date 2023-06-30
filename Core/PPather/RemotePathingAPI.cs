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
using System.Net.Sockets;

namespace Core;

public sealed class RemotePathingAPI : IPPather, IDisposable
{
    private readonly ILogger<RemotePathingAPI> logger;

    private readonly string host = "127.0.0.1";

    private readonly int port = 5001;

    private readonly string api;

    private readonly JsonSerializerOptions options;

    private readonly HttpClient client;

    public RemotePathingAPI(ILogger<RemotePathingAPI> logger,
        string host, int port)
    {
        this.logger = logger;
        this.host = host;
        this.port = port;

        client = new();

        options = new()
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new Vector3Converter());
        options.Converters.Add(new Vector4Converter());

        api = $"http://{host}:{port}/api/PPather/";
    }

    public void Dispose()
    {
        client.Dispose();
    }

    public async ValueTask DrawLines(List<LineArgs> lineArgs)
    {
        using StringContent content = new(JsonSerializer.Serialize(lineArgs, options), Encoding.UTF8, "application/json");
        LogInformation($"Drawing lines '{string.Join(", ", lineArgs.Select(l => l.MapId))}'...");
        await client.PostAsync($"{api}Drawlines", content);
    }

    public async ValueTask DrawSphere(SphereArgs args)
    {
        using StringContent content = new(JsonSerializer.Serialize(args, options), Encoding.UTF8, "application/json");
        await client.PostAsync($"{api}DrawSphere", content);
    }

    public Vector3[] FindMapRoute(int uiMap, Vector3 mapFrom, Vector3 mapTo)
    {
        try
        {
            //LogInformation($"map {uiMap} | {mapFrom} to {mapTo}");
            var url = $"{api}MapRoute?uimap1={uiMap}&x1={mapFrom.X}&y1={mapFrom.Y}&uimap2={uiMap}&x2={mapTo.X}&y2={mapTo.Y}";

            //long timestamp = Stopwatch.GetTimestamp();

            var task = client.GetStringAsync(url);
            string response = task.GetAwaiter().GetResult();

            //LogInformation($"map {uiMap} | {mapFrom} to {mapTo} took {Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds}ms");
            return JsonSerializer.Deserialize<Vector3[]>(response, options) ?? Array.Empty<Vector3>();
        }
        catch (Exception ex)
        {
            LogError($"{mapFrom} to {mapTo}", ex);
            Console.WriteLine(ex);
            return Array.Empty<Vector3>();
        }
    }

    public Vector3[] FindWorldRoute(int uiMap, Vector3 worldFrom, Vector3 worldTo)
    {
        try
        {
            //LogInformation($"map {uiMap} | {worldFrom} map {uiMap} to {worldTo}");
            var url =
                $"{api}WorldRoute2?x1={worldFrom.X}&y1={worldFrom.Y}&z1={worldFrom.Z}&x2={worldTo.X}&y2={worldTo.Y}&z2={worldTo.Z}&uimap={uiMap}";

            //long timestamp = Stopwatch.GetTimestamp();

            string response = client.GetStringAsync(url).GetAwaiter().GetResult();
            //LogInformation($"map {uiMap} | {worldFrom} to {worldTo} took {Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds}ms");
            return JsonSerializer.Deserialize<Vector3[]>(response, options) ?? Array.Empty<Vector3>();
        }
        catch (Exception ex)
        {
            LogError($"{worldFrom} to {worldTo}", ex);
            Console.WriteLine(ex);
            return Array.Empty<Vector3>();
        }
    }

    public bool PingServer()
    {
        try
        {
            using TcpClient client = new(host, port);
            return true;
        }
        catch (SocketException ex)
        {
            LogError($"{nameof(PingServer)} - Connected: false - {api} Gave({ex.Message})");
            return false;
        }
    }

    #region Logging

    private void LogError(string text, Exception? ex = null)
    {
        logger.LogError(ex, text);
    }

    private void LogInformation(string text)
    {
        logger.LogInformation(text);
    }

    #endregion
}