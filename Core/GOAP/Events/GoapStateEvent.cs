namespace Core.GOAP
{
    public class GoapStateEvent : GoapEventArgs
    {
        public GoapKey Key { get; }
        public bool Value { get; }

        public GoapStateEvent(GoapKey key, bool value)
        {
            Key = key;
            Value = value;
        }
    }
}
