using System.Text;

namespace Core
{
    public class BitStatus
    {
        private int value;

        public BitStatus(int value)
        {
            this.value = value;
        }

        public void Update(int v)
        {
            value = v;
        }

        public bool IsBitSet(int pos)
        {
            return (value & (1 << pos)) != 0;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (int i = 1; i < 24; i++)
            {
                sb.Append($"{i}:{IsBitSet(i - 1)},");
            }

            return sb.ToString();
        }
    }
}