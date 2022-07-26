using System;

namespace Core
{
    public class RecordInt
    {
        private readonly int cell;

        public int Value { private set; get; }

        public DateTime LastChanged { private set; get; }

        public int ElapsedMs() => (int)(DateTime.UtcNow - LastChanged).TotalMilliseconds;

        public event Action? Changed;

        public RecordInt(int cell)
        {
            this.cell = cell;
        }

        public bool Updated(AddonDataProvider reader)
        {
            int temp = Value;
            Value = reader.GetInt(cell);

            if (temp != Value)
            {
                Changed?.Invoke();
                LastChanged = DateTime.UtcNow;
                return true;
            }

            return false;
        }

        public bool UpdatedNoEvent(AddonDataProvider reader)
        {
            int temp = Value;
            Value = reader.GetInt(cell);
            return temp != Value;
        }

        public void Update(AddonDataProvider reader)
        {
            int temp = Value;
            Value = reader.GetInt(cell);

            if (temp != Value)
            {
                Changed?.Invoke();
                LastChanged = DateTime.UtcNow;
            }
        }

        public void UpdateTime()
        {
            LastChanged = DateTime.UtcNow;
        }

        public void Reset()
        {
            Value = 0;
            LastChanged = default;
        }

        public void ForceUpdate(int value)
        {
            Value = value;
        }
    }
}