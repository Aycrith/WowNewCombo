using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using System.Net.Http;
using WowheadDB;
using System.Numerics;
using System.Diagnostics;

namespace WowheadDB_Extractor
{
    public class ZoneExtractor
    {
        public const string EXP = "tbc";

        private const string outputPath = "../../../../../Json/area/";
        private const string outputNodePath = "../path/";
        //private const string outputPath = ".";
        private const string ZONE_CLASSIC_URL = "https://classic.wowhead.com/zone=";
        private const string ZONE_TBC_URL = "https://tbc.wowhead.com/zone=";

        public static async Task Run()
        {
            await ExtractZones();
        }

        static async Task ExtractZones()
        {
            // bad
            //Dictionary<string, int> temp = new() { { "Isle of Quel'Danas", 4080 } };

            // test
            //Dictionary<string, int> temp = new() { { "Elwynn Forest", 12 } };
            //Dictionary<string, int> temp = new() { { "Zangarmarsh", 3521 } };
            //foreach (var entry in temp)
            foreach (KeyValuePair<string, int> entry in Areas.List)
            {
                if (entry.Value == 0) continue;
                try
                {
                    var p = GetPayloadFromWebpage(await LoadPage(entry.Value));
                    var z = ZoneFromJson(p);

                    PerZoneGatherable skin = new(entry.Value, GatherFilter.Skinnable);
                    z.skinnable = await skin.Run();

                    PerZoneGatherable g = new(entry.Value, GatherFilter.Gatherable);
                    z.gatherable = await g.Run();

                    PerZoneGatherable m = new(entry.Value, GatherFilter.Minable);
                    z.minable = await m.Run();

                    PerZoneGatherable salv = new(entry.Value, GatherFilter.Salvegable);
                    z.salvegable = await salv.Run();

                    SaveZone(z, entry.Value.ToString());
                    //SaveZoneNode(entry, z.herb, nameof(z.herb), false, true);
                    //SaveZoneNode(entry, z.vein, nameof(z.vein), false, true);

                    Console.WriteLine($"Saved {entry.Value,5}={entry.Key}");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Fail  {entry.Value,5}={entry.Key} -> '{e.Message}'");
                    Console.WriteLine(e);
                }

                await Task.Delay(50);
            }
        }

        static async Task<string> LoadPage(int zoneId)
        {
            var url = ZONE_TBC_URL + zoneId;

            HttpClient client = new HttpClient();
            var response = await client.GetAsync(url);
            return await response.Content.ReadAsStringAsync();
        }

        static string GetPayloadFromWebpage(string content)
        {
            string beginPat = "new ShowOnMap(";
            string endPat = ");</script>";

            int beginPos = content.IndexOf(beginPat);
            int endPos = content.IndexOf(endPat, beginPos);

            return content.Substring(beginPos + beginPat.Length, endPos - beginPos - beginPat.Length);
        }

        static Area ZoneFromJson(string content)
        {
            return JsonConvert.DeserializeObject<Area>(content);
        }

        static void SaveZone(Area zone, string name)
        {
            var output = JsonConvert.SerializeObject(zone);
            var file = Path.Join(outputPath, name + ".json");

            File.WriteAllText(file, output);
        }

        static void SaveZoneNode(KeyValuePair<string, int> zonekvp, Dictionary<string, List<Node>> nodes, string type, bool saveImage, bool onlyOptimalPath)
        {
            if (nodes == null)
                return;

            List<Vector2> points = new();
            foreach (var kvp in nodes)
            {
                points.AddRange(Array.ConvertAll(kvp.Value[0].points.ToArray(), (Vector3 v3) => new Vector2(v3.X, v3.Y)));
            }

            GeneticTSPSolver solver = new(points);
            Stopwatch sw = new();
            sw.Start();
            while (solver.UnchangedGens < solver.Length)
            {
                solver.Evolve();
            }
            sw.Stop();
            Console.WriteLine($" - TSP Solver {points.Count} {type} nodes {sw.ElapsedMilliseconds} ms");

            string prefix = $"{zonekvp.Value}_{zonekvp.Key}_{type}";

            if (saveImage)
                //solver.Draw($"{prefix}.bmp");
                solver.Draw(Path.Join(outputPath, outputNodePath, $"_{type}", $"{prefix}.bmp"));

            if (!onlyOptimalPath)
            {
                var output_points = JsonConvert.SerializeObject(points);
                var file_points = Path.Join(outputPath, outputNodePath, $"_{type}", $"{prefix}.json");
                File.WriteAllText(file_points, output_points);
            }

            var output_tsp = JsonConvert.SerializeObject(solver.Result);
            var file_tsp = Path.Join(outputPath, outputNodePath, $"_{type}", $"{prefix}_optimal.json");
            File.WriteAllText(file_tsp, output_tsp);
        }



        #region local tests

        static void SerializeTest()
        {
            int zoneId = 40;
            var file = Path.Join(outputPath, zoneId + ".json");
            var zone = ZoneFromJson(File.ReadAllText(file));
        }

        static void ExtractFromFileTest()
        {
            var file = Path.Join(outputPath, "a.html");
            var html = File.ReadAllText(file);

            string payload = GetPayloadFromWebpage(html);
            var zone = ZoneFromJson(payload);
        }

        #endregion

    }
}
