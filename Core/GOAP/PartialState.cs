namespace Core.GOAP
{
    public readonly struct PartialState
    {
        public readonly GoapKey Key;
        public readonly bool Value;

        public PartialState(GoapKey key, bool value)
        {
            Key = key;
            Value = value;
        }
    }
}
