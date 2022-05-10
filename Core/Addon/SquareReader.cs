using System.Runtime.CompilerServices;

namespace Core
{
    public class SquareReader
    {
        private readonly AddonReader addonReader;

        public SquareReader(AddonReader addonReader)
        {
            this.addonReader = addonReader;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetInt(int index)
        {
            return addonReader.GetInt(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetFixed(int index)
        {
            return GetInt(index) / 100000f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetString(int index)
        {
            int color = GetInt(index);
            if (color != 0)
            {
                string colorString = color.ToString();
                if (colorString.Length > 6) { return string.Empty; }
                string colorText = "000000"[..(6 - colorString.Length)] + colorString;
                return ToChar(colorText, 0) + ToChar(colorText, 2) + ToChar(colorText, 4);
            }
            else
            {
                return string.Empty;
            }
        }

        private static string ToChar(string colorText, int start)
        {
            return ((char)int.Parse(colorText.Substring(start, 2))).ToString();
        }
    }
}