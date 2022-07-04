using Core.GOAP;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Core.Goals
{
    public abstract class GoapGoal
    {
        public Dictionary<GoapKey, bool> Preconditions { get; } = new();
        public Dictionary<GoapKey, bool> Effects { get; } = new();
        public Dictionary<GoapKey, bool> State { get; private set; } = new();

        private KeyAction[] keys = Array.Empty<KeyAction>();
        public KeyAction[] Keys
        {
            get => keys;
            protected set
            {
                keys = value;
                if (keys.Length == 1)
                    DisplayName = $"{Keys[0].Name} [{Keys[0].Key}]";
            }
        }

        public abstract float Cost { get; }

        public string Name { get; }

        public string DisplayName { get; protected set; }

        public event Action<GoapEventArgs>? GoapEvent;

        protected GoapGoal(string name)
        {
            string output = Regex.Replace(name.Replace("Goal", ""), @"\p{Lu}", m => " " + m.Value.ToUpperInvariant());
            DisplayName = Name = string.Concat(output[0].ToString().ToUpper(), output.AsSpan(1));
        }

        public void SendGoapEvent(GoapEventArgs e)
        {
            GoapEvent?.Invoke(e);
        }

        public void SetState(Dictionary<GoapKey, bool> newState)
        {
            State = newState;
        }

        public virtual bool CanRun()
        {
            return true;
        }

        public virtual void OnEnter() { }

        public virtual void OnExit() { }

        public virtual void Update() { }

        public void AddPrecondition(GoapKey key, bool value)
        {
            Preconditions[key] = value;
        }

        public void RemovePrecondition(GoapKey key)
        {
            Preconditions.Remove(key);
        }

        public void AddEffect(GoapKey key, bool value)
        {
            Effects[key] = value;
        }

        public void RemoveEffect(GoapKey key)
        {
            Effects.Remove(key);
        }
    }
}