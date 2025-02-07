using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using SharedLib.Extensions;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

using WowheadDB;

using static System.IO.File;
using static System.IO.Path;

namespace Core.Database;

public enum NPCType
{
    None,
    Flightmaster,
    Innkeeper,
    Repair,
    Vendor,
    Trainer
}

public static class NPCType_Extension
{
    public static string ToStringF(this NPCType value) => value switch
    {
        NPCType.None => nameof(NPCType.None),
        NPCType.Innkeeper => nameof(NPCType.Innkeeper),
        NPCType.Flightmaster => nameof(NPCType.Flightmaster),
        NPCType.Repair => nameof(NPCType.Repair),
        NPCType.Vendor => nameof(NPCType.Vendor),
        NPCType.Trainer => nameof(NPCType.Trainer),
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };
}

public sealed class AreaDB : IDisposable
{
    private readonly ILogger logger;
    private readonly DataConfig dataConfig;

    private readonly CancellationToken token;
    private readonly ManualResetEventSlim resetEvent;
    private readonly Thread thread;

    private int areaId = -1;
    public Area? CurrentArea { private set; get; }

    public event Action? Changed;

    public AreaDB(ILogger logger, DataConfig dataConfig,
        CancellationTokenSource cts)
    {
        this.logger = logger;
        this.dataConfig = dataConfig;
        token = cts.Token;
        resetEvent = new();

        thread = new(ReadArea);
        thread.Start();
    }

    public void Dispose()
    {
        resetEvent.Set();
    }

    public void Update(int areaId)
    {
        if (this.areaId == areaId)
            return;

        this.areaId = areaId;
        resetEvent.Set();
    }

    private void ReadArea()
    {
        resetEvent.Wait();

        while (!token.IsCancellationRequested)
        {
            try
            {
                CurrentArea = JsonConvert.DeserializeObject<Area>(
                    ReadAllText(Join(dataConfig.ExpArea, $"{areaId}.json")));

                Changed?.Invoke();
            }
            catch (Exception e)
            {
                logger.LogError(e.Message, e.StackTrace);
            }

            resetEvent.Reset();
            resetEvent.Wait();
        }
    }

    private List<NPC> GetNPCs(NPCType type)
    {
        return type switch
        {
            NPCType.Flightmaster => CurrentArea?.flightmaster,
            NPCType.Innkeeper => CurrentArea?.innkeeper,
            NPCType.Repair => CurrentArea?.repair,
            NPCType.Vendor => CurrentArea?.vendor,
            NPCType.Trainer => CurrentArea?.trainer,
            _ => null
        } ?? [];
    }

    public NPC? GetNearestNPC(PlayerFaction faction, NPCType type, Vector3 map)
    {
        List<NPC> npcs = GetNPCs(type);

        if (CurrentArea == null || npcs.Count == 0)
            return null;

        NPC? closestNpc = null;
        float mapDistance = float.MaxValue;

        for (int i = 0; i < npcs.Count; i++)
        {
            NPC npc = npcs[i];

            // Those Vendor NPCS whom are class specific
            // Demon Trainer example
            if (type != NPCType.Trainer &&
                npc.description != null &&
                npc.description.Contains(NPCType.Trainer.ToStringF()))
            {
                continue;
            }

            if (npc.MapCoords.Length == 0)
                continue;

            float d = map.MapDistanceXYTo(npc.MapCoords[0]);
            if (d < mapDistance && FriendlyToPlayer(npc, faction))
            {
                mapDistance = d;
                closestNpc = npc;
            }
        }

        return closestNpc;

        static bool FriendlyToPlayer(NPC npc, PlayerFaction playerFaction) =>
            playerFaction switch
            {
                PlayerFaction.Alliance => npc.reactalliance == 1,
                PlayerFaction.Horde => npc.reacthorde == 1,
                _ => false
            };
    }
}