using Microsoft.Extensions.Logging;

using SharedLib.Converters;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Hazard;

public sealed class LocalHazardDAO
{
    private const string CurrentVersion = "1.0";

    private readonly DataConfig dataConfig;
    private readonly ILogger<LocalHazardDAO> logger;
    private readonly JsonSerializerOptions jsonOptions;

    public LocalHazardDAO(DataConfig dataConfig, ILogger<LocalHazardDAO> logger)
    {
        this.dataConfig = dataConfig;
        this.logger = logger;

        jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        jsonOptions.Converters.Add(new Vector3Converter());
    }

    public async Task SaveAsync(
        string expansion,
        int mapId,
        IReadOnlyList<HazardEvent> events,
        CancellationToken cancellationToken)
    {
        string directory = GetExpansionDirectory(expansion);
        Directory.CreateDirectory(directory);

        string filePath = Path.Combine(directory, $"hazards_{mapId}.json");

        HazardDataFile file = new()
        {
            Version = CurrentVersion,
            MapId = mapId,
            LastUpdated = DateTime.UtcNow,
            Events = events.Count == 0 ? [] : [.. events]
        };

        await using FileStream stream = new(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            useAsync: true);

        await JsonSerializer.SerializeAsync(stream, file, jsonOptions, cancellationToken);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("[LocalHazardDAO     ] Saved {Count} events to {File}", events.Count, filePath);
        }
    }

    public async Task<IReadOnlyList<HazardEvent>> LoadAsync(
        string expansion,
        int mapId,
        CancellationToken cancellationToken)
    {
        string filePath = Path.Combine(GetExpansionDirectory(expansion), $"hazards_{mapId}.json");
        if (!File.Exists(filePath))
        {
            return [];
        }

        try
        {
            await using FileStream stream = new(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                useAsync: true);

            HazardDataFile? file = await JsonSerializer.DeserializeAsync<HazardDataFile>(stream, jsonOptions, cancellationToken);
            return file?.Events ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[LocalHazardDAO     ] Failed to load hazard file {File}", filePath);
            return [];
        }
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlyList<HazardEvent>>> LoadAllAsync(
        string expansion,
        CancellationToken cancellationToken)
    {
        string directory = GetExpansionDirectory(expansion);
        if (!Directory.Exists(directory))
        {
            return new Dictionary<int, IReadOnlyList<HazardEvent>>();
        }

        string[] files = Directory.GetFiles(directory, "hazards_*.json");
        if (files.Length == 0)
        {
            return new Dictionary<int, IReadOnlyList<HazardEvent>>();
        }

        Dictionary<int, IReadOnlyList<HazardEvent>> result = new(files.Length);

        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];
            string name = Path.GetFileNameWithoutExtension(file);
            int underscore = name.LastIndexOf('_');
            if (underscore < 0 || !int.TryParse(name[(underscore + 1)..], out int mapId))
            {
                continue;
            }

            IReadOnlyList<HazardEvent> events = await LoadAsync(expansion, mapId, cancellationToken);
            result[mapId] = events;
        }

        return result;
    }

    private string GetExpansionDirectory(string expansion)
    {
        string exp = string.IsNullOrWhiteSpace(expansion)
            ? dataConfig.Exp
            : expansion.Trim().ToLowerInvariant();

        return Path.Combine(dataConfig.Root, "HazardData", exp);
    }

    private sealed class HazardDataFile
    {
        public string Version { get; init; } = CurrentVersion;
        public int MapId { get; init; }
        public DateTime LastUpdated { get; init; } = DateTime.UtcNow;
        public HazardEvent[] Events { get; init; } = [];
    }
}

