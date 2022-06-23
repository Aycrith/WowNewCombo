using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using SharedLib;

namespace ReadDBC_CSV
{
    public class WorldMapAreaExtractor : IExtractor
    {
        private readonly string path;

        public List<string> FileRequirement { get; } = new List<string>()
        {
            "uimap.csv",
            "uimapassignment.csv",
            "map.csv"
        };

        public WorldMapAreaExtractor(string path)
        {
            this.path = path;
        }

        public void Run()
        {
            // UIMapId - AreaName
            string uimapFile = Path.Join(path, FileRequirement[0]);
            List<WorldMapArea> wmas = ExtractUIMap(uimapFile);

            // MapID - AreaID - LocBottom - LocRight - LocTop - LocLeft
            string uimapassignmentFile = Path.Join(path, FileRequirement[1]);
            ExtractBoundaries(uimapassignmentFile, wmas);

            // Continent / Directory
            string mapFile = Path.Join(path, FileRequirement[2]);
            ExtractContinent(mapFile, wmas);

            ClearEmptyBound(wmas);

            Console.WriteLine($"WMAs: {wmas.Count}");
            File.WriteAllText(Path.Join(path, "WorldMapArea.json"), JsonConvert.SerializeObject(wmas, Formatting.Indented));
        }

        private static List<WorldMapArea> ExtractUIMap(string path)
        {
            int idIndex = -1;
            int nameIndex = -1;
            int systemIndex = -1;

            CSVExtractor extractor = new();
            extractor.HeaderAction = () =>
            {
                idIndex = extractor.FindIndex("ID");
                nameIndex = extractor.FindIndex("Name_lang");
                systemIndex = extractor.FindIndex("System");
            };

            List<WorldMapArea> items = new();
            void extractLine(string[] values)
            {
                int uiMapId = int.Parse(values[idIndex]);
                int system = int.Parse(values[systemIndex]);

                // 1 ([DEPRECATED] Legacy Taxi)
                if (system == 1)
                {
                    return;
                }

                items.Add(new WorldMapArea
                {
                    UIMapId = uiMapId,
                    AreaName = values[nameIndex],
                });
            }

            extractor.ExtractTemplate(path, extractLine);
            return items;
        }

        private static void ExtractBoundaries(string path, List<WorldMapArea> wmas)
        {
            int uiMapIdIndex = -1;
            int mapIdIndex = -1;
            int areaIdIndex = -1;

            int orderIndexIndex = -1;

            int region0Index = -1;
            int region1Index = -1;
            int region3Index = -1;
            int region4Index = -1;

            CSVExtractor extractor = new();
            extractor.HeaderAction = () =>
            {
                uiMapIdIndex = extractor.FindIndex("UiMapID");
                mapIdIndex = extractor.FindIndex("MapID");
                areaIdIndex = extractor.FindIndex("AreaID");

                orderIndexIndex = extractor.FindIndex("OrderIndex");

                region0Index = extractor.FindIndex("Region[0]");
                region1Index = extractor.FindIndex("Region[1]");

                region3Index = extractor.FindIndex("Region[3]");
                region4Index = extractor.FindIndex("Region[4]");
            };

            void extractLine(string[] values)
            {
                int uiMapId = int.Parse(values[uiMapIdIndex]);
                int orderIndex = int.Parse(values[orderIndexIndex]);

                int index = wmas.FindIndex(x => x.UIMapId == uiMapId && orderIndex == 0);
                if (index > -1)
                {
                    WorldMapArea wma = wmas[index];
                    wmas[index] = new WorldMapArea
                    {
                        MapID = int.Parse(values[mapIdIndex]),
                        AreaID = int.Parse(values[areaIdIndex]),

                        AreaName = wma.AreaName,

                        LocBottom = float.Parse(values[region0Index]),
                        LocRight = float.Parse(values[region1Index]),

                        LocTop = float.Parse(values[region3Index]),
                        LocLeft = float.Parse(values[region4Index]),

                        UIMapId = wma.UIMapId,
                        Continent = wma.Continent,
                    };
                }
            }

            extractor.ExtractTemplate(path, extractLine);
        }

        private static void ExtractContinent(string path, List<WorldMapArea> wmas)
        {
            int mapIdIndex = -1;
            int directoryIndex = -1;

            CSVExtractor extractor = new();
            extractor.HeaderAction = () =>
            {
                mapIdIndex = extractor.FindIndex("ID");
                directoryIndex = extractor.FindIndex("Directory", 1);
            };

            void extractLine(string[] values)
            {
                int mapId = int.Parse(values[mapIdIndex]);
                string directory = values[directoryIndex];

                for (int i = 0; i < wmas.Count; i++)
                {
                    if (wmas[i].MapID != mapId)
                        continue;

                    WorldMapArea wma = wmas[i];
                    wmas[i] = new WorldMapArea
                    {
                        MapID = wma.MapID,
                        AreaID = wma.AreaID,

                        AreaName = wma.AreaName,

                        LocBottom = wma.LocBottom,
                        LocRight = wma.LocRight,

                        LocTop = wma.LocTop,
                        LocLeft = wma.LocLeft,

                        UIMapId = wma.UIMapId,
                        Continent = directory
                    };
                }
            }

            extractor.ExtractTemplate(path, extractLine);
        }

        private static void ClearEmptyBound(List<WorldMapArea> wmas)
        {
            for (int i = wmas.Count - 1; i >= 0; i--)
            {
                if (wmas[i].LocBottom == 0 &&
                    wmas[i].LocLeft == 0 &&
                    wmas[i].LocRight == 0 &&
                    wmas[i].LocTop == 0)
                {
                    wmas.RemoveAt(i);
                }
            }
        }
    }
}
