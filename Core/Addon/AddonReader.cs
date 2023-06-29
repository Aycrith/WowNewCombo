using Core.Database;

using Microsoft.Extensions.Logging;

using SharedLib;

using System;
using System.Threading;

namespace Core;

public sealed class AddonReader : IAddonReader, IDisposable
{
    private readonly ILogger logger;
    private readonly IAddonDataProvider reader;
    private readonly AutoResetEvent resetEvent;

    private readonly PlayerReader PlayerReader;
    private readonly CombatLog combatLog;
    private readonly BagReader bagReader;
    private readonly EquipmentReader equipmentReader;

    private readonly ActionBarCostReader actionBarCostReader;
    private readonly ActionBarCooldownReader actionBarCooldownReader;

    private readonly ActionBarBits<ICurrentAction> currentAction;
    private readonly ActionBarBits<IUsableAction> usableAction;

    private readonly GossipReader gossipReader;

    private readonly SpellBookReader spellBookReader;
    private readonly TalentReader talentReader;

    private readonly WorldMapAreaDB worldMapAreaDb;
    private readonly CreatureDB creatureDb;
    private readonly AreaDB areaDb;

    public AuraTimeReader<IPlayerBuffTimeReader> PlayerBuffTimeReader { get; }
    public AuraTimeReader<ITargetDebuffTimeReader> TargetDebuffTimeReader { get; }
    public AuraTimeReader<ITargetBuffTimeReader> TargetBuffTimeReader { get; }

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

    public AddonReader(ILogger logger, IAddonDataProvider reader, 
        PlayerReader playerReader, AutoResetEvent resetEvent,
        AreaDB areaDB, WorldMapAreaDB worldMapAreaDB, CreatureDB creatureDB,
        CombatLog combatLog,
        EquipmentReader equipmentReader, BagReader bagReader,
        GossipReader gossipReader, SpellBookReader spellBookReader,
        TalentReader talentReader,
        ActionBarCostReader actionBarCostReader,
        ActionBarCooldownReader actionBarCooldownReader,
        ActionBarBits<ICurrentAction> currentAction,
        ActionBarBits<IUsableAction> usableAction,
        AuraTimeReader<IPlayerBuffTimeReader> playerBuffTimeReader,
        AuraTimeReader<ITargetDebuffTimeReader> targetDebuffTimeReader,
        AuraTimeReader<ITargetBuffTimeReader> targetBuffTimeReader
        )
    {
        this.logger = logger;
        this.reader = reader;
        this.resetEvent = resetEvent;

        this.areaDb = areaDB;
        this.worldMapAreaDb = worldMapAreaDB;
        this.creatureDb = creatureDB;

        this.combatLog = combatLog;

        this.equipmentReader = equipmentReader;
        this.bagReader = bagReader;

        this.actionBarCostReader = actionBarCostReader;
        this.actionBarCooldownReader = actionBarCooldownReader;

        this.gossipReader = gossipReader;

        this.spellBookReader = spellBookReader;

        this.PlayerReader = playerReader;
        this.talentReader = talentReader;

        this.currentAction = currentAction;
        this.usableAction = usableAction;

        this.PlayerBuffTimeReader = playerBuffTimeReader;
        this.TargetDebuffTimeReader = targetDebuffTimeReader;
        this.TargetBuffTimeReader = targetBuffTimeReader;

        lastUpdate = DateTime.UtcNow;
    }

    public void Dispose()
    {
        bagReader.Dispose();
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
        else if (updateIndex >= 512)
        {
            updateSum = 0;
            updateIndex = 0;
        }

        resetEvent.Reset();

        updateSum += (DateTime.UtcNow - lastUpdate).TotalMilliseconds;
        updateIndex++;
        AvgUpdateLatency = updateSum / updateIndex;
        lastUpdate = DateTime.UtcNow;

        IAddonDataProvider reader = this.reader;

        currentAction.Update(reader);
        usableAction.Update(reader);

        PlayerReader.Update(reader);

        if (lastTargetGuid != PlayerReader.TargetGuid)
        {
            lastTargetGuid = PlayerReader.TargetGuid;

            TargetName =
                creatureDb.Entries.TryGetValue(PlayerReader.TargetId, out string? name)
                ? name
                : reader.GetString(16) + reader.GetString(17);
        }

        if (lastMouseOverId != PlayerReader.MouseOverId)
        {
            lastMouseOverId = PlayerReader.MouseOverId;
            MouseOverName =
                creatureDb.Entries.TryGetValue(PlayerReader.MouseOverId, out string? name)
                ? name
                : string.Empty;
        }

        combatLog.Update(reader, PlayerReader.Bits.PlayerInCombat());

        bagReader.Read(reader);
        equipmentReader.Read(reader);

        actionBarCostReader.Read(reader);
        actionBarCooldownReader.Read(reader);

        gossipReader.Read(reader);

        spellBookReader.Read(reader);
        talentReader.Read(reader);

        PlayerBuffTimeReader.Read(reader);
        TargetDebuffTimeReader.Read(reader);
        TargetBuffTimeReader.Read(reader);

        areaDb.Update(worldMapAreaDb.GetAreaId(PlayerReader.UIMapId.Value));

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
        PlayerReader.Reset();

        actionBarCostReader.Reset();
        actionBarCooldownReader.Reset();
        spellBookReader.Reset();
        talentReader.Reset();

        PlayerBuffTimeReader.Reset();
        TargetDebuffTimeReader.Reset();
        TargetBuffTimeReader.Reset();

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