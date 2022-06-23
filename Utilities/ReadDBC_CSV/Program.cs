using System;
using System.IO;
using System.Net;

namespace ReadDBC_CSV
{
    public enum Locale
    {
        enUS,
        koKR,
        frFR,
        deDE,
        zhCN,
        esES,
        zhTW,
        enGB,
        esMX,
        ruRU,
        ptBR,
        itIT
    }

    class Program
    {
        static string userAgent = " Mozilla/5.0 (Windows NT 6.1; WOW64; rv:25.0) Gecko/20100101 Firefox/25.0";

        static Locale locale = Locale.enUS;
        static string path = "../../../data/";
        static string build = "2.5.4.44171"; // TBCC 2.5.2.40617 // TBCC latest 2.5.4.44171 // WOTLK alpha 3.4.0.44064

        static void Main(string[] args)
        {
            GenerateItems(path);
            GenerateConsumables(path);
            GenerateSpells(path);
            GenerateTalents(path);
            GenerateWorldMapArea(path);
        }

        private static void GenerateItems(string path)
        {
            ItemExtractor extractor = new(path);
            DownloadRequirements(path, extractor, build);
            extractor.Run();
        }

        private static void GenerateConsumables(string path)
        {
            string foodDesc = "Restores $o1 health over $d";
            string waterDesc = "mana over $d";

            if (Version.TryParse(build, out Version version) && version.Major == 1)
            {
                waterDesc = "Restores $o1 mana over $d";
            }

            ConsumablesExtractor extractor = new(path, foodDesc, waterDesc);
            DownloadRequirements(path, extractor, build);
            extractor.Run();
        }

        private static void GenerateSpells(string path)
        {
            SpellExtractor extractor = new(path);
            DownloadRequirements(path, extractor, build);
            extractor.Run();
        }

        private static void GenerateTalents(string path)
        {
            TalentExtractor extractor = new(path);
            DownloadRequirements(path, extractor, build);
            extractor.Run();
        }

        private static void GenerateWorldMapArea(string path)
        {
            WorldMapAreaExtractor extractor = new(path);
            DownloadRequirements(path, extractor, build);
            extractor.Run();
        }


        #region Download files

        private static void DownloadRequirements(string path, IExtractor extractor, params string[] builds)
        {
            foreach (string file in extractor.FileRequirement)
            {
                if (File.Exists(Path.Join(path, file)))
                {
                    Console.WriteLine($"{file} already exists. Skip downloading.");
                    continue;
                }

                foreach (string build in builds)
                {
                    using WebClient client = new();
                    client.Headers.Add("user-agent", userAgent);

                    try
                    {
                        string url = DownloadURL(build, file);
                        client.DownloadFile(url, Path.Join(path, file));

                        Console.WriteLine($"{file} - {build} - Downloaded - {url}");

                        break;
                    }
                    catch (Exception e)
                    {
                        if (File.Exists(Path.Join(path, file)))
                        {
                            File.Delete(Path.Join(path, file));
                        }

                        Console.WriteLine($"{file} - {build} - {e.Message}");
                    }
                }
            }
        }

        private static string DownloadURL(string build, string file)
        {
            string resource = file.Split(".")[0];
            return $"https://wow.tools/dbc/api/export/?name={resource}&build={build}&locale={locale}";
        }

        #endregion
    }
}
