using System.Collections.Generic;
using System;
using Core.Database;
using SharedLib;

namespace Core;

public sealed class SpellBookReader : IReader
{
    private const int cSpellId = 71;

    private readonly HashSet<int> spells = new();

    public SpellDB SpellDB { get; }
    public int Count => spells.Count;

    public SpellBookReader(SpellDB spellDB)
    {
        this.SpellDB = spellDB;
    }

    public void Update(IAddonDataProvider reader)
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
                name.Contains(spell.Name, StringComparison.OrdinalIgnoreCase))
            {
                return spell.Id;
            }
        }

        return 0;
    }
}
