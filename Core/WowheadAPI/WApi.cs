using System.Threading.Tasks;
using System.Xml;

namespace Core
{
    public static class WApi
    {
        public static ClientVersion Version
        {
            set
            {
                switch (value)
                {
                    case ClientVersion.SoM:
                        BaseUrl = "https://classic.wowhead.com";
                        break;
                    case ClientVersion.TBC:
                        BaseUrl = "https://tbc.wowhead.com";
                        break;
                    case ClientVersion.Wrath:
                        BaseUrl = "https://www.wowhead.com/wotlk";
                        break;
                    default:
                    case ClientVersion.Retail:
                        BaseUrl = "https://www.wowhead.com";
                        break;
                }
            }
        }

        public static string BaseUrl { get; set; } = "https://www.wowhead.com";

        public static string NpcId => $"{BaseUrl}/npc=";
        public static string ItemId => $"{BaseUrl}/item=";
        public static string SpellId => $"{BaseUrl}/spell=";

        public const string TinyIconUrl = "https://wow.zamimg.com/images/wow/icons/tiny/{0}.gif";
        public const string MedIconUrl = "https://wow.zamimg.com/images/wow/icons/medium/{0}.jpg";

        public static async Task<string> FetchItemIconName(int itemId)
        {
            try
            {
                using XmlReader reader = XmlReader.Create($"{ItemId}{itemId}&xml", new XmlReaderSettings { Async = true, LineNumberOffset = 14 });
                while (await reader.ReadAsync())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name.Contains("icon"))
                    {
                        await reader.ReadAsync();
                        return reader.Value;
                    }
                }
            }
            catch { }

            return string.Empty;
        }
    }
}