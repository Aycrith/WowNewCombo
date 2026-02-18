using Core.FeatureFlags;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Hazard;

public sealed class HazardAnalyticsBackgroundService : BackgroundService
{
    private readonly HazardZoneStore store;
    private readonly HazardClusterAnalyzer analyzer;
    private readonly LocalHazardDAO dao;
    private readonly FeatureFlagService featureFlagService;
    private readonly DataConfig dataConfig;
    private readonly ILogger<HazardAnalyticsBackgroundService> logger;

    public HazardAnalyticsBackgroundService(
        HazardZoneStore store,
        HazardClusterAnalyzer analyzer,
        LocalHazardDAO dao,
        FeatureFlagService featureFlagService,
        DataConfig dataConfig,
        ILogger<HazardAnalyticsBackgroundService> logger)
    {
        this.store = store;
        this.analyzer = analyzer;
        this.dao = dao;
        this.featureFlagService = featureFlagService;
        this.dataConfig = dataConfig;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await LoadExistingHazards(stoppingToken);

        Task clusteringLoop = RunClusteringLoop(stoppingToken);
        Task saveLoop = RunSaveLoop(stoppingToken);

        await Task.WhenAll(clusteringLoop, saveLoop);
    }

    private async Task LoadExistingHazards(CancellationToken stoppingToken)
    {
        try
        {
            string expansion = dataConfig.Exp;
            IReadOnlyDictionary<int, IReadOnlyList<HazardEvent>> all = await dao.LoadAllAsync(expansion, stoppingToken);

            foreach (KeyValuePair<int, IReadOnlyList<HazardEvent>> kvp in all)
            {
                store.AddEvents(kvp.Key, kvp.Value);
            }

            logger.LogInformation(
                "[HazardAnalytics   ] Loaded hazard history for {Expansion} ({MapCount} maps)",
                expansion, all.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[HazardAnalytics   ] Failed to load hazard history");
        }
    }

    private async Task RunClusteringLoop(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            FeatureFlagsOptions options = featureFlagService.Current;
            HazardAvoidanceOptions hazardOptions = options.HazardAvoidance;

            int seconds = Math.Clamp(hazardOptions.ClusteringIntervalSeconds, 5, 3600);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!hazardOptions.Enabled)
            {
                continue;
            }

            try
            {
                IReadOnlyList<int> mapIds = store.GetKnownMapIds();
                for (int i = 0; i < mapIds.Count; i++)
                {
                    int mapId = mapIds[i];
                    IReadOnlyList<HazardEvent> events = store.GetEventsSnapshot(mapId);
                    if (events.Count == 0)
                    {
                        continue;
                    }

                    List<HazardCluster> clusters = analyzer.RunDBSCAN(
                        events,
                        hazardOptions.DBSCANEpsilon,
                        hazardOptions.DBSCANMinPoints,
                        hazardOptions.DecayHalfLifeDays);

                    store.ReplaceClusters(mapId, clusters);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[HazardAnalytics   ] Clustering loop failed");
            }
        }
    }

    private async Task RunSaveLoop(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            FeatureFlagsOptions options = featureFlagService.Current;
            HazardAvoidanceOptions hazardOptions = options.HazardAvoidance;

            int minutes = Math.Clamp(hazardOptions.SaveIntervalMinutes, 1, 1440);

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(minutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!hazardOptions.Enabled)
            {
                continue;
            }

            try
            {
                string expansion = dataConfig.Exp;
                IReadOnlyList<int> mapIds = store.GetKnownMapIds();
                for (int i = 0; i < mapIds.Count; i++)
                {
                    int mapId = mapIds[i];
                    IReadOnlyList<HazardEvent> events = store.GetEventsSnapshot(mapId);
                    await dao.SaveAsync(expansion, mapId, events, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[HazardAnalytics   ] Save loop failed");
            }
        }
    }
}

