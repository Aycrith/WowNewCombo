using System;
using System.Collections.Generic;

namespace Core
{
    public sealed class CombatLog
    {
        private bool wasInCombat;

        public event Action? KillCredit;

        public HashSet<int> DamageDone { get; } = new();
        public HashSet<int> DamageTaken { get; } = new();

        public RecordInt DamageDoneGuid { get; }
        public RecordInt DamageTakenGuid { get; }
        public RecordInt DeadGuid { get; }

        public RecordInt TargetMissType { get; }
        public RecordInt TargetDodge { get; }

        public CombatLog(int cDamageDone, int cDamageTaken, int cDead, int cTargetMiss)
        {
            DamageDoneGuid = new RecordInt(cDamageDone);
            DamageTakenGuid = new RecordInt(cDamageTaken);
            DeadGuid = new RecordInt(cDead);

            TargetMissType = new(cTargetMiss);
            TargetDodge = new(cTargetMiss);
        }

        public void Reset()
        {
            wasInCombat = false;

            DamageDone.Clear();
            DamageTaken.Clear();

            DamageDoneGuid.Reset();
            DamageTakenGuid.Reset();
            DeadGuid.Reset();

            TargetMissType.Reset();
            TargetDodge.Reset();
        }

        public void Update(IAddonDataProvider reader, bool playerInCombat)
        {
            if (TargetMissType.Updated(reader) && (MissType)TargetMissType.Value == MissType.DODGE)
            {
                TargetDodge.UpdateTime();
            }

            if (playerInCombat && DamageTakenGuid.Updated(reader) && DamageTakenGuid.Value > 0)
            {
                DamageTaken.Add(DamageTakenGuid.Value);
            }

            if (playerInCombat && DamageDoneGuid.Updated(reader) && DamageDoneGuid.Value > 0)
            {
                DamageDone.Add(DamageDoneGuid.Value);
            }

            if (DeadGuid.Updated(reader) && DeadGuid.Value > 0)
            {
                int deadGuid = DeadGuid.Value;
                DamageDone.Remove(deadGuid);
                DamageTaken.Remove(deadGuid);

                KillCredit?.Invoke();
            }

            if (wasInCombat && !playerInCombat)
            {
                // left combat
                DamageTaken.Clear();
                DamageDone.Clear();
            }

            wasInCombat = playerInCombat;
        }
    }
}