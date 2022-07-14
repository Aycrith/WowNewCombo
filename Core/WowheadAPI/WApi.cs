using System.Threading.Tasks;
using System.Xml;

namespace Core
{
    public static class WApi
    {
        public const string Version = "tbc";

        public const string NpcId = $"https://{Version}.wowhead.com/npc=";
        public const string ItemId = $"https://{Version}.wowhead.com/item=";
        public const string SpellId = $"https://{Version}.wowhead.com/spell=";
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