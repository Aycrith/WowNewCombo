using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using SharedLib;
using SharedLib.Data;
using SharedLib.Extensions;

using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;

using WowheadDB;

using static System.IO.File;
using static System.IO.Path;

namespace Core.Database;

public sealed class AreaDB : IDisposable
{
    private readonly ILogger logger;
    private readonly DataConfig dataConfig;

    private readonly CreatureDB creatures;
    private readonly WorldMapAreaDB worldMapAreaDB;
    private readonly FactionTemplateDB factionDB;

    private readonly CancellationToken token;
    private readonly AsyncManualResetEvent resetEvent;

    private readonly JsonSerializerSettings npcJsonSettings = new()
    {
        StringEscapeHandling = StringEscapeHandling.EscapeNonAscii
    };

    private int areaId = -1;

    public FrozenDictionary<int, Vector3[]> NpcWorldLocations { private set; get; } = FrozenDictionary<int, Vector3[]>.Empty;
    public Area? CurrentArea { private set; get; }
    public WorldMapArea? CurrentWorldMapArea { private set; get; }
    public WorldMapArea? Hitbox { private set; get; }

    public event Action? Changed;

    public AreaDB(ILogger logger, DataConfig dataConfig,
        CreatureDB creatures,
        WorldMapAreaDB worldMapAreaDB,
        CancellationTokenSource cts,
        FactionTemplateDB factionDB)
    {
        this.logger = logger;
        this.dataConfig = dataConfig;
        this.creatures = creatures;
        this.factionDB = factionDB;
        this.worldMapAreaDB = worldMapAreaDB;

        token = cts.Token;
        resetEvent = new(false);

        Task.Run(ReadAreaAsync, token);
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

    private async Task ReadAreaAsync()
    {
        await resetEvent.WaitAsync();

        while (!token.IsCancellationRequested)
        {
            try
            {
                CurrentArea = JsonConvert.DeserializeObject<Area>(
                    ReadAllText(Join(dataConfig.ExpArea, $"{areaId}.json")));

                CurrentWorldMapArea = worldMapAreaDB.GetByAreaId(areaId);

                Hitbox = worldMapAreaDB.GetByAreaIdHit(areaId);

                var data = JsonConvert.DeserializeObject<Dictionary<int, Vector3[]>>(
                    ReadAllText(Join(dataConfig.NpcSpawnLocations, $"{CurrentWorldMapArea.Value.MapID}.json")), npcJsonSettings);

                NpcWorldLocations = data != null
                    ? data.ToFrozenDictionary()
                    : FrozenDictionary<int, Vector3[]>.Empty;

                Changed?.Invoke();
            }
            catch (Exception e)
            {
                logger.LogError(e.Message, e.StackTrace);
            }

            resetEvent.Reset();
            await resetEvent.WaitAsync();
        }
    }

    public ReadOnlySpan<Creature> GetByNpcFlag(NpcFlags flag)
    {
        if (CurrentArea == null)
            return [];

        List<Creature> npc = [..
            creatures.Entries.Values
            .Where(x => x.NpcFlag.Has(flag))
            ];

        return CollectionsMarshal.AsSpan(npc);
    }

    public int GetNearestNpcs(
        PlayerFaction faction,
        NpcFlags type,
        Vector3 playerPosW,
        string[] allowedNames,
        Span<NpcSearchResult> destination, // caller-provided buffer
        out int written)
    {
        written = 0;

        ReadOnlySpan<Creature> npcs = GetByNpcFlag(type);
        var pool = ArrayPool<NpcSearchResult>.Shared;
        NpcSearchResult[] rented = pool.Rent(npcs.Length * 2); // worst case: multiple positions per npc
        int count = 0;

        try
        {
            foreach (var n in npcs)
            {
                if (allowedNames.Length != 0 && !allowedNames.Contains(n.Name))
                    continue;

                if (!NpcWorldLocations.TryGetValue(n.Entry, out Vector3[]? worldPos))
                    continue;

                foreach (var pos in worldPos)
                {
                    // Skip entries with missing elevation data (Z=0 causes pathfinder to route into building geometry)
                    if (pos.Z == 0)
                        continue;

                    var mapPos = WorldMapAreaDB.ToMap_FlipXY(pos, Hitbox!.Value);
                    if (mapPos.X <= 0 || mapPos.X >= 100 || mapPos.Y <= 0 || mapPos.Y >= 100)
                        continue;

                    if (!FriendlyToPlayer(n, faction, factionDB))
                        continue;

                    float d = playerPosW.WorldDistanceXYTo(pos);
                    if (count < rented.Length)
                    {
                        rented[count++] = new NpcSearchResult(n, pos, d);
                    }
                }
            }

            Array.Sort(rented, 0, count, Comparer<NpcSearchResult>.Create(
                static (a, b) => a.Distance.CompareTo(b.Distance)));

            int toCopy = Math.Min(count, destination.Length);
            rented.AsSpan(0, toCopy).CopyTo(destination);
            written = toCopy;

            return count;
        }
        finally
        {
            pool.Return(rented, clearArray: false);
        }
    }

    public int GetNearestAreaServiceCandidates(
        PlayerFaction faction,
        NpcServiceKind service,
        Vector3 playerPosW,
        string[] allowedNames,
        Span<NpcServiceCandidate> destination,
        out int written)
    {
        if (CurrentArea == null || CurrentWorldMapArea == null)
        {
            written = 0;
            return 0;
        }

        return CollectCuratedServiceCandidates(
            CurrentArea,
            CurrentWorldMapArea.Value,
            NpcWorldLocations,
            faction,
            service,
            playerPosW,
            allowedNames,
            destination,
            out written);
    }

    internal static int CollectCuratedServiceCandidates(
        Area area,
        in WorldMapArea worldMapArea,
        FrozenDictionary<int, Vector3[]> npcWorldLocations,
        PlayerFaction faction,
        NpcServiceKind service,
        Vector3 playerPosW,
        string[] allowedNames,
        Span<NpcServiceCandidate> destination,
        out int written)
    {
        written = 0;

        List<NPC>? serviceNpcs = GetAreaServiceNpcs(area, service);
        if (serviceNpcs == null || serviceNpcs.Count == 0)
        {
            return 0;
        }

        var pool = ArrayPool<NpcServiceCandidate>.Shared;
        NpcServiceCandidate[] rented = pool.Rent(serviceNpcs.Count);
        int count = 0;

        try
        {
            for (int i = 0; i < serviceNpcs.Count; i++)
            {
                NPC npc = serviceNpcs[i];

                if (allowedNames.Length != 0 &&
                    !allowedNames.Contains(npc.name, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!FriendlyToPlayer(npc, faction) || ShouldSkipCuratedServiceNpc(npc, service))
                {
                    continue;
                }

                if (!TryResolveCuratedServiceWorldPosition(npc, npcWorldLocations, playerPosW, worldMapArea, out Vector3 worldPosition, out Vector3 mapPosition))
                {
                    continue;
                }

                rented[count++] = new NpcServiceCandidate(
                    service,
                    NpcServiceCandidateSource.AreaCurated,
                    npc.id,
                    npc.name,
                    worldPosition,
                    mapPosition,
                    GetServiceFlags(service),
                    npc.description ?? string.Empty);
            }

            Array.Sort(rented, 0, count, Comparer<NpcServiceCandidate>.Create(
                (a, b) => playerPosW.WorldDistanceXYTo(a.WorldPosition).CompareTo(playerPosW.WorldDistanceXYTo(b.WorldPosition))));

            int toCopy = Math.Min(count, destination.Length);
            rented.AsSpan(0, toCopy).CopyTo(destination);
            written = toCopy;
            return count;
        }
        finally
        {
            pool.Return(rented, clearArray: false);
        }
    }

    static bool FriendlyToPlayer(Creature npc, PlayerFaction playerFaction, FactionTemplateDB factionDB)
    {
        if (!factionDB.Factions.TryGetValue(npc.Faction, out int friendGroup))
            return false;

        const int AllPlayers = 1;

        const int AlliancePlayers = 2;
        int allianceOurMask = AllPlayers | AlliancePlayers;

        const int HordePlayers = 4;
        int hordeOurMask = AllPlayers | HordePlayers;

        return playerFaction switch
        {
            PlayerFaction.Alliance => (friendGroup & allianceOurMask) != 0,
            PlayerFaction.Horde => (friendGroup & hordeOurMask) != 0,
            _ => false
        };
    }

    internal static bool FriendlyToPlayer(NPC npc, PlayerFaction playerFaction)
    {
        return playerFaction switch
        {
            PlayerFaction.Alliance => npc.reactalliance == 1,
            PlayerFaction.Horde => npc.reacthorde == 1,
            _ => false
        };
    }

    internal static bool ShouldSkipCuratedServiceNpc(NPC npc, NpcServiceKind service)
    {
        return service != NpcServiceKind.Trainer &&
            !string.IsNullOrWhiteSpace(npc.description) &&
            npc.description.Contains(NpcServiceKind.Trainer.ToStringF(), StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TryResolveCuratedServiceWorldPosition(
        NPC npc,
        FrozenDictionary<int, Vector3[]> npcWorldLocations,
        Vector3 playerPosW,
        in WorldMapArea worldMapArea,
        out Vector3 worldPosition,
        out Vector3 mapPosition)
    {
        if (npcWorldLocations.TryGetValue(npc.id, out Vector3[]? spawnWorldPositions) &&
            spawnWorldPositions.Length > 0)
        {
            float bestDistance = float.MaxValue;
            Vector3 bestWorldPosition = Vector3.Zero;

            for (int i = 0; i < spawnWorldPositions.Length; i++)
            {
                Vector3 candidateWorldPosition = spawnWorldPositions[i];
                if (candidateWorldPosition.Z == 0)
                {
                    continue;
                }

                float distance = playerPosW.WorldDistanceXYTo(candidateWorldPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestWorldPosition = candidateWorldPosition;
                }
            }

            if (bestDistance < float.MaxValue)
            {
                worldPosition = bestWorldPosition;
                mapPosition = WorldMapAreaDB.ToMap_FlipXY(bestWorldPosition, worldMapArea);
                return true;
            }
        }

        if (npc.MapCoords.Length == 0)
        {
            worldPosition = Vector3.Zero;
            mapPosition = Vector3.Zero;
            return false;
        }

        mapPosition = npc.MapCoords[0];
        worldPosition = WorldMapAreaDB.ToWorld_FlipXY(mapPosition, worldMapArea);
        return worldPosition != Vector3.Zero;
    }

    internal static List<NPC>? GetAreaServiceNpcs(Area area, NpcServiceKind service)
    {
        return service switch
        {
            NpcServiceKind.Vendor => area.vendor,
            NpcServiceKind.Repair => area.repair,
            NpcServiceKind.Innkeeper => area.innkeeper,
            NpcServiceKind.Trainer => area.trainer,
            NpcServiceKind.FlightMaster => area.flightmaster,
            _ => null
        };
    }

    internal static NpcFlags GetServiceFlags(NpcServiceKind service)
    {
        return service switch
        {
            NpcServiceKind.Vendor => NpcFlags.Vendor,
            NpcServiceKind.Repair => NpcFlags.Repair | NpcFlags.Vendor,
            NpcServiceKind.Innkeeper => NpcFlags.Innkeeper,
            NpcServiceKind.Trainer => NpcFlags.Trainer | NpcFlags.ClassTrainer | NpcFlags.ProfessionTrainer,
            NpcServiceKind.FlightMaster => NpcFlags.FlightMaster,
            _ => NpcFlags.None
        };
    }

    public (Creature, Vector3) FindClosestCreatureByNpcFlag(NpcFlags npcFlag, Vector3 position)
    {
        Creature closest = default;
        float closestDistance = float.MaxValue;
        Vector3 closestWorldPos = default;

        foreach ((int id, Creature creature) in creatures.Entries)
        {
            if (!creature.NpcFlag.HasFlag(npcFlag))
                continue;

            if (!NpcWorldLocations.TryGetValue(id, out Vector3[]? worldPos))
                continue;

            Vector3 firstWorldPos = worldPos[0];

            float distance = firstWorldPos.WorldDistanceXYTo(position);
            if (distance < closestDistance)
            {
                closestWorldPos = firstWorldPos;
                closestDistance = distance;
                closest = creature;
            }
        }
        return (closest, closestWorldPos);
    }

    public bool TryGetCreature(int entry, [MaybeNullWhen(false)] out Creature creature)
    {
        return creatures.Entries.TryGetValue(entry, out creature);
    }
}
