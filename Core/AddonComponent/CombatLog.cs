using System;
using System.Collections.Generic;

namespace Core
{
    public class CombatLog
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

        public void Update(AddonDataProvider reader, bool playerInCombat)
        {
            if (TargetMissType.Updated(reader) && (MissType)TargetMissType.Value == MissType.DODGE)
            {
                TargetDodge.UpdateTime();
            }

            if (DamageTakenGuid.Updated(reader) && DamageTakenGuid.Value > 0)
            {
                DamageTaken.Add(DamageTakenGuid.Value);
            }

            if (DamageDoneGuid.Updated(reader) && DamageDoneGuid.Value > 0)
            {
                DamageDone.Add(DamageDoneGuid.Value);
            }

            if (DeadGuid.Updated(reader) && DeadGuid.Value > 0)
            {
                bool killCredit = false;
                int deadGuid = DeadGuid.Value;

                if (DamageTaken.Contains(deadGuid))
                {
                    killCredit = true;
                }

                if (DamageDone.Contains(deadGuid))
                {
                    killCredit = true;
                }

                DamageDone.Remove(deadGuid);
                DamageTaken.Remove(deadGuid);

                if (killCredit)
                {
                    KillCredit?.Invoke();
                }
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