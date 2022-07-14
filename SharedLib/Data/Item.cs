using System;

namespace SharedLib
{
    public readonly struct Item
    {
        public int Entry { get; init; }
        public string Name { get; init; }
        public int Quality { get; init; }
        public int SellPrice { get; init; }

        public static int[] ToSellPrice(int sellPrice)
        {
            if (sellPrice == 0) { return new int[3] { 0, 0, 0 }; }

            int sign = sellPrice < 0 ? -1 : 1;

            int gold = 0;
            int silver = 0;
            int copper = Math.Abs(sellPrice);

            if (copper >= 10000)
            {
                gold = copper / 10000;
                copper %= 10000;
            }

            if (copper >= 100)
            {
                silver = copper / 100;
                copper %= 100;
            }

            return new int[3] { sign * gold, sign * silver, sign * copper };
        }
    }
}