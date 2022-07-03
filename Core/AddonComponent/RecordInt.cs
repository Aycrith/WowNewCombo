using System;

namespace Core
{
    public class RecordInt
    {
        private readonly int cell;

        private int v;
        public int Value
        {
            private set
            {
                if (v != value)
                {
                    Changed?.Invoke();
                    LastChanged = DateTime.UtcNow;
                }

                v = value;
            }

            get => v;
        }
        public DateTime LastChanged { private set; get; }

        public int ElapsedMs() => (int)(DateTime.UtcNow - LastChanged).TotalMilliseconds;

        public event Action? Changed;

        public RecordInt(int cell)
        {
            this.cell = cell;
        }

        public bool Updated(AddonDataProvider reader)
        {
            int temp = v;
            Value = reader.GetInt(cell);
            return v != temp;
        }

        public void Update(AddonDataProvider reader)
        {
            Value = reader.GetInt(cell);
        }

        public void UpdateTime()
        {
            LastChanged = DateTime.UtcNow;
        }

        public void Reset()
        {
            v = 0;
            LastChanged = default;
        }

        public void ForceUpdate(int value)
        {
            v = value;
        }
    }
}