using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Frontend;

/// <summary>
/// Persists Pathing configuration to <c>appsettings.json</c>.
/// Changes require a server restart to re-bind the pathing backend.
/// </summary>
public sealed class PathingAdminService
{
    private static readonly object FileGate = new();

    private readonly ILogger<PathingAdminService> logger;
    private readonly IWebHostEnvironment env;

    public PathingAdminService(
        ILogger<PathingAdminService> logger,
        IWebHostEnvironment env)
    {
        this.logger = logger;
        this.env = env;
    }

    public string AppSettingsFilePath => Path.Combine(env.ContentRootPath, "appsettings.json");

    public sealed record PathingSettings(
        string Mode,
        string HostV1,
        int PortV1,
        string HostV3,
        int PortV3,
        bool PathVisualizer);

    public void Save(PathingSettings settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        if (string.IsNullOrWhiteSpace(settings.Mode))
        {
            throw new ArgumentException("Mode is required.", nameof(settings));
        }

        lock (FileGate)
        {
            JsonObject root = LoadOrCreateRootObject(AppSettingsFilePath);

            JsonObject pathing = root["Pathing"] as JsonObject ?? new JsonObject();
            pathing["Mode"] = settings.Mode;
            pathing["hostv1"] = settings.HostV1 ?? string.Empty;
            pathing["portv1"] = settings.PortV1;
            pathing["hostv3"] = settings.HostV3 ?? string.Empty;
            pathing["portv3"] = settings.PortV3;
            pathing["PathVisualizer"] = settings.PathVisualizer;

            root["Pathing"] = pathing;

            string json = root.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            });

            WriteFileAtomic(AppSettingsFilePath, json);
        }

        logger.LogInformation(
            "[PathingAdmin     ] Saved {File} Mode={Mode} V1={HostV1}:{PortV1} V3={HostV3}:{PortV3} Viz={Viz}",
            Path.GetFileName(AppSettingsFilePath),
            settings.Mode,
            settings.HostV1,
            settings.PortV1,
            settings.HostV3,
            settings.PortV3,
            settings.PathVisualizer);
    }

    private static JsonObject LoadOrCreateRootObject(string path)
    {
        if (!File.Exists(path))
        {
            return new JsonObject();
        }

        try
        {
            string existing = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(existing))
            {
                return new JsonObject();
            }

            JsonNode? node = JsonNode.Parse(existing);
            return node as JsonObject ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }

    private static void WriteFileAtomic(string path, string content)
    {
        string dir = Path.GetDirectoryName(path) ?? string.Empty;
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }
}

