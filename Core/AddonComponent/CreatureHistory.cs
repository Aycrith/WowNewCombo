using System;
using System.Collections.Generic;

namespace Core
{
    public class CreatureHistory
    {
        private readonly SquareReader reader;

        private const double LifeTimeInSec = 60;

        public event Action? KillCredit;

        public List<CreatureRecord> Creatures { get; } = new();
        public List<CreatureRecord> Targets { get; } = new();
        public List<CreatureRecord> DamageDone { get; } = new();
        public List<CreatureRecord> DamageTaken { get; } = new();
        public List<CreatureRecord> Deads { get; } = new();

        public RecordInt CombatCreatureGuid { get; }
        public RecordInt CombatDamageDoneGuid { get; }
        public RecordInt CombatDamageTakenGuid { get; }
        public RecordInt CombatDeadGuid { get; }

        public CreatureHistory(SquareReader reader, int cCreature, int cDamageDone, int cDamageTaken, int cDead)
        {
            this.reader = reader;

            CombatCreatureGuid = new RecordInt(cCreature);
            CombatDamageDoneGuid = new RecordInt(cDamageDone);
            CombatDamageTakenGuid = new RecordInt(cDamageTaken);
            CombatDeadGuid = new RecordInt(cDead);
        }

        public void Reset()
        {
            Creatures.Clear();
            Targets.Clear();
            DamageDone.Clear();
            DamageTaken.Clear();
            Deads.Clear();

            CombatCreatureGuid.Reset();
            CombatDamageDoneGuid.Reset();
            CombatDamageTakenGuid.Reset();
            CombatDeadGuid.Reset();
        }

        public void Update(int targetGuid, int targetHealthPercent)
        {
            Update(targetGuid, targetHealthPercent, Targets);

            if (CombatCreatureGuid.Updated(reader))
            {
                Update(CombatCreatureGuid.Value, 100f, Creatures);
            }

            if (CombatDamageTakenGuid.Updated(reader))
            {
                Update(CombatDamageTakenGuid.Value, 100f, DamageTaken);
            }

            if (CombatDamageDoneGuid.Updated(reader))
            {
                Update(CombatDamageDoneGuid.Value, 100f, DamageDone);
            }

            if (CombatDeadGuid.Updated(reader))
            {
                Update(CombatDeadGuid.Value, 0, Creatures);
                Update(CombatDeadGuid.Value, 0, Deads);

                if (DamageTaken.Exists(x => x.Guid == CombatDeadGuid.Value))
                {
                    Update(CombatDeadGuid.Value, 0, DamageTaken);
                }

                if (DamageDone.Exists(x => x.Guid == CombatDeadGuid.Value))
                {
                    Update(CombatDeadGuid.Value, 0, DamageDone);
                }

                if (Targets.Exists(x => x.Guid == CombatDeadGuid.Value))
                {
                    Update(CombatDeadGuid.Value, 0, Targets);
                }

                if (Targets.Exists(x => x.Guid == CombatDeadGuid.Value) &&
                    (DamageDone.Exists(x => x.Guid == CombatDeadGuid.Value) || DamageTaken.Exists(x => x.Guid == CombatDeadGuid.Value)))
                {
                    KillCredit?.Invoke();
                }
            }

            RemoveExpired(Targets);
            RemoveExpired(Creatures);
            RemoveExpired(DamageTaken);
            RemoveExpired(DamageDone);
            RemoveExpired(Deads);
        }

        private static void Update(int creatureId, float healthPercent, List<CreatureRecord> creatures)
        {
            if (creatureId <= 0) return;

            int index = creatures.FindIndex(c => c.Guid == creatureId);
            if (index > -1)
            {
                if (healthPercent < creatures[index].HealthPercent)
                {
                    CreatureRecord creature = creatures[index];

                    creature.HealthPercent = healthPercent;
                    creature.LastEvent = DateTime.UtcNow;

                    creatures[index] = creature;
                }
            }
            else
            {
                creatures.Add(new CreatureRecord
                {
                    Guid = creatureId,
                    HealthPercent = healthPercent,
                    LastEvent = DateTime.UtcNow
                });
            }
        }

        private static void RemoveExpired(List<CreatureRecord> creatures)
        {
            DateTime now = DateTime.UtcNow;
            for (int i = creatures.Count - 1; i >= 0; i--)
            {
                if ((now - creatures[i].LastEvent).TotalSeconds > LifeTimeInSec)
                {
                    creatures.RemoveAt(i);
                }
            }
        }
    }
}