using System.Collections.Generic;

namespace Core
{
    public class GossipReader
    {
        private readonly int cGossip;

        public int Count { private set; get; }
        public Dictionary<Gossip, int> Gossips { get; } = new();

        private int data;

        public bool Ready => Gossips.Count == Count;

        public bool GossipStart() => data == 69;
        public bool GossipEnd() => data == 9999994;

        public bool MerchantWindowOpened() => data == 9999999;

        public bool MerchantWindowClosed() => data == 9999998;

        public bool MerchantWindowSelling() => data == 9999997;

        public bool MerchantWindowSellingFinished() => data == 9999996;

        public bool GossipStartOrMerchantWindowOpened() => GossipStart() || MerchantWindowOpened();

        public GossipReader(int cGossip)
        {
            this.cGossip = cGossip;
        }

        public void Read(AddonDataProvider reader)
        {
            data = reader.GetInt(cGossip);

            // used for merchant window open state
            if (MerchantWindowClosed() ||
                MerchantWindowOpened() ||
                MerchantWindowSelling() ||
                MerchantWindowSellingFinished() ||
                GossipEnd())
                return;

            if (data == 0 || GossipStart())
            {
                Count = 0;
                Gossips.Clear();
                return;
            }

            // formula
            // 10000 * count + 100 * index + value
            int count = (int)(data / 10000f);
            data -= 10000 * count;

            int order = (int)(data / 100f);
            data -= 100 * order;

            Count = count;

            if (!Gossips.ContainsKey((Gossip)data))
            {
                Gossips.Add((Gossip)data, order);
            }
        }

    }
}
