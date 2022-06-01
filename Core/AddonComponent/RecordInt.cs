using System;

namespace Core
{
    public class RecordInt
    {
        private readonly int cell;
        private int temp;

        public int Value { private set; get; }
        public DateTime LastChanged { private set; get; }

        public int ElapsedMs => (int)(DateTime.UtcNow - LastChanged).TotalMilliseconds;

        public event Action? Changed;

        public RecordInt(int cell)
        {
            this.cell = cell;
        }

        public bool Updated(AddonDataProvider reader)
        {
            temp = reader.GetInt(cell);
            if (temp != Value)
            {
                Value = temp;
                Changed?.Invoke();
                LastChanged = DateTime.UtcNow;
                return true;
            }

            return false;
        }

        public void Update(AddonDataProvider reader)
        {
            temp = reader.GetInt(cell);
            if (temp != Value)
            {
                Value = temp;
                Changed?.Invoke();
                LastChanged = DateTime.UtcNow;
            }
        }

        public void Reset()
        {
            Value = 0;
            temp = 0;
            LastChanged = default;
        }

        public void ForceUpdate(int value)
        {
            Value = value;
        }
    }
}