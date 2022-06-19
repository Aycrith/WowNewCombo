using System;
using System.IO;
using Newtonsoft.Json;
using Core.Talents;
using SharedLib;

namespace Core.Database
{
    public class TalentDB
    {
        private readonly SpellDB spellDB;

        private readonly TalentTab[] talentTabs;
        private readonly TalentTreeElement[] talentTreeElements;

        public TalentDB(DataConfig dataConfig, SpellDB spellDB)
        {
            this.spellDB = spellDB;

            talentTabs = JsonConvert.DeserializeObject<TalentTab[]>(File.ReadAllText(Path.Join(dataConfig.Dbc, "talenttab.json")));
            talentTreeElements = JsonConvert.DeserializeObject<TalentTreeElement[]>(File.ReadAllText(Path.Join(dataConfig.Dbc, "talent.json")));
        }

        public bool Update(ref Talent talent, PlayerClassEnum playerClassEnum)
        {
            string playerClass = playerClassEnum.ToStringF();

            int tabIndex = talent.TabNum - 1;
            int talentTabIndex = -1;
            for (int i = 0; i < talentTabs.Length; i++)
            {
                if (talentTabs[i].BackgroundFile.Contains(playerClass, StringComparison.OrdinalIgnoreCase) &&
                    talentTabs[i].OrderIndex == tabIndex)
                {
                    talentTabIndex = i;
                    break;
                }
            }
            if (talentTabIndex == -1) return false;

            int tierIndex = talent.TierNum - 1;
            int columnIndex = talent.ColumnNum - 1;
            int rankIndex = talent.CurrentRank - 1;

            int talentElementIndex = -1;
            for (int i = 0; i < talentTreeElements.Length; i++)
            {
                if (talentTreeElements[i].TabID == talentTabs[talentTabIndex].Id &&
                    talentTreeElements[i].TierID == tierIndex &&
                    talentTreeElements[i].ColumnIndex == columnIndex)
                {
                    talentElementIndex = i;
                    break;
                }
            }

            int spellId = talentTreeElements[talentElementIndex].SpellIds[rankIndex];
            if (spellDB.Spells.TryGetValue(spellId, out Spell spell))
            {
                talent.Name = spell.Name;
                return true;
            }

            return false;
        }
    }
}
