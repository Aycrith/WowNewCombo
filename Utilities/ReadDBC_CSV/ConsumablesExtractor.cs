using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace ReadDBC_CSV
{
    public class ConsumablesExtractor : IExtractor
    {
        private readonly string path;

        private readonly string foodDesc;
        private readonly string waterDesc;

        public List<string> FileRequirement { get; } = new()
        {
            "spell.csv",
            "itemeffect.csv",
        };

        public ConsumablesExtractor(string path, string foodDesc, string waterDesc)
        {
            this.path = path;

            this.foodDesc = foodDesc;
            this.waterDesc = waterDesc;
        }

        public void Run()
        {
            string spellFile = Path.Join(path, FileRequirement[0]);

            List<int> foodSpells = ExtractSpells(spellFile, foodDesc);
            List<int> waterSpells = ExtractSpells(spellFile, waterDesc);

            string itemEffectFile = Path.Join(path, FileRequirement[1]);

            List<int> foodIds = ExtractItem(itemEffectFile, foodSpells);
            foodIds.Sort();
            Console.WriteLine($"Foods: {foodIds.Count}");
            File.WriteAllText(Path.Join(path, "foods.json"), JsonConvert.SerializeObject(foodIds));

            List<int> waterIds = ExtractItem(itemEffectFile, waterSpells);
            waterIds.Sort();
            Console.WriteLine($"Waters: {waterIds.Count}");
            File.WriteAllText(Path.Join(path, "waters.json"), JsonConvert.SerializeObject(waterIds));
        }

        private static List<int> ExtractSpells(string path, string descLang)
        {
            int entryIndex = -1;
            int descIndex = -1;

            CSVExtractor extractor = new();
            extractor.HeaderAction = () =>
            {
                entryIndex = extractor.FindIndex("ID");
                descIndex = extractor.FindIndex("Description_lang");
            };

            List<int> items = new();
            void extractLine(string[] values)
            {
                if (values[descIndex].Contains(descLang))
                {
                    items.Add(int.Parse(values[entryIndex]));
                }
            }

            extractor.ExtractTemplate(path, extractLine);
            return items;
        }

        private static List<int> ExtractItem(string path, List<int> spells)
        {
            int entryIndex = -1;
            int spellIdIndex = -1;
            int ParentItemIDIndex = -1;

            CSVExtractor extractor = new();
            extractor.HeaderAction = () =>
            {
                entryIndex = extractor.FindIndex("ID");
                spellIdIndex = extractor.FindIndex("SpellID", 7);
                ParentItemIDIndex = extractor.FindIndex("ParentItemID", 9);
            };

            List<int> items = new();
            void extractLine(string[] values)
            {
                int spellId = int.Parse(values[spellIdIndex]);
                if (spells.Contains(spellId))
                {
                    int ItemId = int.Parse(values[ParentItemIDIndex]);
                    items.Add(ItemId);
                }
            }

            extractor.ExtractTemplate(path, extractLine);

            return items;
        }

    }
}
