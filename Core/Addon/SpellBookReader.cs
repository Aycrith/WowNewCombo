using System.Collections.Generic;
using System;
using Core.Database;
using SharedLib;

namespace Core
{
    public class SpellBookReader
    {
        private readonly int cSpellId;

        private readonly HashSet<int> spells = new();

        public SpellDB SpellDB { get; }
        public int Count => spells.Count;

        public SpellBookReader(int cSpellId, SpellDB spellDB)
        {
            this.cSpellId = cSpellId;
            this.SpellDB = spellDB;
        }

        public void Read(AddonDataProvider reader)
        {
            int spellId = reader.GetInt(cSpellId);
            if (spellId == 0) return;

            spells.Add(spellId);
        }

        public void Reset()
        {
            spells.Clear();
        }

        public bool Has(int id)
        {
            return spells.Contains(id);
        }

        public bool TryGetValue(int id, out Spell spell)
        {
            return SpellDB.Spells.TryGetValue(id, out spell);
        }

        public int GetId(string name)
        {
            foreach (int id in spells)
            {
                if (TryGetValue(id, out Spell spell) &&
                    name.Equals(spell.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return spell.Id;
                }
            }

            return 0;
        }
    }
}
