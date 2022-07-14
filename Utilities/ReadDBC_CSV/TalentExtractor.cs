using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using SharedLib;

namespace ReadDBC_CSV
{
    public class TalentExtractor : IExtractor
    {
        private readonly string path;

        public List<string> FileRequirement { get; } = new List<string>()
        {
            "talenttab.csv",
            "talent.csv"
        };

        public TalentExtractor(string path)
        {
            this.path = path;
        }

        public void Run()
        {
            string talenttabFile = Path.Join(path, FileRequirement[0]);
            List<TalentTab> talenttabs = ExtractTalentTabs(talenttabFile);
            Console.WriteLine($"TalentTabs: {talenttabs.Count}");
            File.WriteAllText(Path.Join(path, "talenttab.json"), JsonConvert.SerializeObject(talenttabs));

            string talentFile = Path.Join(path, FileRequirement[1]);
            List<TalentTreeElement> talents = ExtractTalentTrees(talentFile);
            Console.WriteLine($"Talents: {talents.Count}");
            File.WriteAllText(Path.Join(path, "talent.json"), JsonConvert.SerializeObject(talents));
        }

        private static List<TalentTab> ExtractTalentTabs(string path)
        {
            int idIndex = -1;
            int orderIndex = -1;
            int classMaskIndex = -1;

            CSVExtractor extractor = new();
            extractor.HeaderAction = () =>
            {
                idIndex = extractor.FindIndex("ID");
                orderIndex = extractor.FindIndex("OrderIndex");
                classMaskIndex = extractor.FindIndex("ClassMask");
            };

            List<TalentTab> talenttabs = new();
            void extractLine(string[] values)
            {
                talenttabs.Add(new TalentTab
                {
                    Id = int.Parse(values[idIndex]),
                    OrderIndex = int.Parse(values[orderIndex]),
                    ClassMask = int.Parse(values[classMaskIndex])
                });
            }

            extractor.ExtractTemplate(path, extractLine);

            return talenttabs;
        }

        public static List<TalentTreeElement> ExtractTalentTrees(string path)
        {
            int idIndex = -1;

            int tierIDIndex = -1;
            int columnIndex = -1;
            int tabIDIndex = -1;

            int spellRank0Index = -1;
            int spellRank1Index = -1;
            int spellRank2Index = -1;
            int spellRank3Index = -1;
            int spellRank4Index = -1;

            CSVExtractor extractor = new();
            extractor.HeaderAction = () =>
            {
                idIndex = extractor.FindIndex("ID");

                tierIDIndex = extractor.FindIndex("TierID");
                columnIndex = extractor.FindIndex("ColumnIndex");
                tabIDIndex = extractor.FindIndex("TabID");

                spellRank0Index = extractor.FindIndex("SpellRank[0]");
                spellRank1Index = extractor.FindIndex("SpellRank[1]");
                spellRank2Index = extractor.FindIndex("SpellRank[2]");
                spellRank3Index = extractor.FindIndex("SpellRank[3]");
                spellRank4Index = extractor.FindIndex("SpellRank[4]");
            };


            List<TalentTreeElement> talents = new();
            void extractLine(string[] values)
            {
                //Console.WriteLine($"{values[entryIndex]} - {values[nameIndex]}");
                talents.Add(new TalentTreeElement
                {
                    TierID = int.Parse(values[tierIDIndex]),
                    ColumnIndex = int.Parse(values[columnIndex]),
                    TabID = int.Parse(values[tabIDIndex]),

                    SpellIds = new int[]
                    {
                        int.Parse(values[spellRank0Index]),
                        int.Parse(values[spellRank1Index]),
                        int.Parse(values[spellRank2Index]),
                        int.Parse(values[spellRank3Index]),
                        int.Parse(values[spellRank4Index])
                    }
                });
            }

            extractor.ExtractTemplate(path, extractLine);

            return talents;
        }

    }
}
