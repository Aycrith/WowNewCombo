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
    private readonly ManualResetEventSlim resetEvent;
    private readonly Thread thread;

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
            resetEvent.Wait();
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

    public bool TryGetNearestNPC(
        PlayerFaction faction,
        NpcFlags type,
        Vector3 playerPosW,
        string[] allowedNames,
        [MaybeNullWhen(false)] out Creature npc,
        out Vector3 pos)
    {
        npc = default;
        pos = default;

        float distance = float.MaxValue;

        ReadOnlySpan<Creature> npcs = GetByNpcFlag(type);
        foreach (var n in npcs)
        {
            if (allowedNames.Length != 0 && !allowedNames.Contains(n.Name))
                continue;

            if (!NpcWorldLocations.TryGetValue(n.Entry, out Vector3[]? worldPos))
                continue;

            var firstWorldPos = worldPos[0];

            float d = playerPosW.WorldDistanceXYTo(firstWorldPos);
            if (d < distance && FriendlyToPlayer(n, faction, factionDB))
            {
                pos = firstWorldPos;
                distance = d;
                npc = n;
            }
        }

        if (npc == default)
        {
            return false;
        }

        var mapPos = WorldMapAreaDB.ToMap_FlipXY(pos, Hitbox!.Value);

        return
            mapPos.X > 0 && mapPos.X < 100 &&
            mapPos.Y > 0 && mapPos.Y < 100;

        static bool FriendlyToPlayer(Creature npc, PlayerFaction playerFaction, FactionTemplateDB factionDB)
        {
            int friendGroup = factionDB.Factions[npc.Faction];

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

            float distance = Vector3.DistanceSquared(firstWorldPos, position);
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