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
            StringBuilder sb = new();
            for (int i = 1; i < ActionBar.BIT_PER_CELL; i++)
            {
                sb.Append(i);
                sb.Append(':');
                sb.Append(IsBitSet(i - 1));
                sb.Append(',');
            }

            return sb.ToString();
        }
    }
}