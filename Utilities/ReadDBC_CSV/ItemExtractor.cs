using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using SharedLib;

namespace ReadDBC_CSV
{
    public class ItemExtractor : IExtractor
    {
        private readonly string path;

        public List<string> FileRequirement { get; } = new()
        {
            "itemsparse.csv"
        };

        public ItemExtractor(string path)
        {
            this.path = path;
        }

        public void Run()
        {
            string fileName = Path.Join(path, FileRequirement[0]);
            List<Item> items = ExtractItems(fileName);

            Console.WriteLine($"Items: {items.Count}");
            File.WriteAllText(Path.Join(path, "items.json"), JsonConvert.SerializeObject(items));
        }

        private static List<Item> ExtractItems(string path)
        {
            int idIndex = -1;
            int nameIndex = -1;
            int qualityIndex = -1;
            int sellPriceIndex = -1;

            CSVExtractor extractor = new();
            extractor.HeaderAction = () =>
            {
                idIndex = extractor.FindIndex("ID");
                nameIndex = extractor.FindIndex("Display_lang");
                qualityIndex = extractor.FindIndex("OverallQualityID");
                sellPriceIndex = extractor.FindIndex("SellPrice");
            };

            List<Item> items = new();
            void extractLine(string[] values)
            {
                items.Add(new Item
                {
                    Entry = int.Parse(values[idIndex]),
                    Quality = int.Parse(values[qualityIndex]),
                    Name = values[nameIndex],
                    SellPrice = int.Parse(values[sellPriceIndex])
                });
            }

            extractor.ExtractTemplate(path, extractLine);
            return items;
        }
    }
}
