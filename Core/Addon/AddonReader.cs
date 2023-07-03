using Core.Database;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SharedLib;

using System;
using System.Collections.Immutable;
using System.Threading;

namespace Core;

public sealed class AddonReader : IAddonReader
{
    private readonly IAddonDataProvider reader;
    private readonly AutoResetEvent resetEvent;

    private readonly PlayerReader playerReader;
    private readonly CreatureDB creatureDb;

    private readonly CombatLog combatLog;

    private readonly ImmutableArray<IReader> readers;

    public event Action? AddonDataChanged;

    public RecordInt GlobalTime { get; } = new(98);

    private int lastTargetGuid = -1;
    public string TargetName { get; private set; } = string.Empty;

    private int lastMouseOverId = -1;
    public string MouseOverName { get; private set; } = string.Empty;

    public double AvgUpdateLatency { private set; get; }
    private double updateSum;
    private int updateIndex;
    private DateTime lastUpdate;

    public AddonReader(IAddonDataProvider reader,
        PlayerReader playerReader, AutoResetEvent resetEvent,
        CreatureDB creatureDb,
        CombatLog combatLog,
        IServiceProvider sp)
    {
        this.reader = reader;
        this.resetEvent = resetEvent;
        this.creatureDb = creatureDb;
        this.combatLog = combatLog;
        this.playerReader = playerReader;

        readers = sp.GetServices<IReader>().ToImmutableArray();

        lastUpdate = DateTime.UtcNow;
    }

    public void Update()
    {
        FetchData();

        if (!GlobalTime.UpdatedNoEvent(this.reader))
            return;

        if (GlobalTime.Value <= 3)
        {
            updateSum = 0;
            updateIndex = 0;

            FullReset();
            return;
        }
        else if (updateIndex >= 8)
        {
            updateSum = 0;
            updateIndex = 0;
        }

        resetEvent.Reset();

        updateSum += (DateTime.UtcNow - lastUpdate).TotalMilliseconds;
        updateIndex++;
        AvgUpdateLatency = updateSum / updateIndex;

        IAddonDataProvider reader = this.reader;

        ReadOnlySpan<IReader> span = readers.AsSpan();
        for (int i = 0; i < span.Length; i++)
        {
            span[i].Update(reader);
        }

        if (lastTargetGuid != playerReader.TargetGuid)
        {
            lastTargetGuid = playerReader.TargetGuid;

            TargetName =
                creatureDb.Entries.TryGetValue(playerReader.TargetId, out string? name)
                ? name
                : reader.GetString(16) + reader.GetString(17);
        }

        if (lastMouseOverId != playerReader.MouseOverId)
        {
            lastMouseOverId = playerReader.MouseOverId;
            MouseOverName =
                creatureDb.Entries.TryGetValue(playerReader.MouseOverId, out string? name)
                ? name
                : string.Empty;
        }

        lastUpdate = DateTime.UtcNow;

        resetEvent.Set();
    }

    public void FetchData()
    {
        reader.Update();
    }

    public void SessionReset()
    {
        combatLog.Reset();
    }

    public void FullReset()
    {
        ReadOnlySpan<IReader> span = readers.AsSpan();
        for (int i = 0; i < span.Length; i++)
        {
            span[i].Reset();
        }

        SessionReset();
    }

    public int GetInt(int index)
    {
        return reader.GetInt(index);
    }

    public void UpdateUI()
    {
        AddonDataChanged?.Invoke();
    }
}