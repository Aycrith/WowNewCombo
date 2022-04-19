using Core.GOAP;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Core.Goals
{
    public class ActionEventArgs : EventArgs
    {
        public GoapKey Key { get; }
        public object Value { get; }

        public ActionEventArgs(GoapKey key, object value)
        {
            this.Key = key;
            this.Value = value;
        }
    }

    public abstract class GoapGoal
    {
        public Dictionary<GoapKey, bool> Preconditions { get; } = new();
        public Dictionary<GoapKey, bool> Effects { get; } = new();
        public Dictionary<GoapKey, bool> State { get; private set; } = new();

        public List<KeyAction> Keys { get; } = new();

        public abstract float CostOfPerformingAction { get; }

        private string name = string.Empty;

        public virtual string Name
        {
            get
            {
                if (string.IsNullOrEmpty(name))
                {
                    string output = Regex.Replace(this.GetType().Name.Replace("Action", ""), @"\p{Lu}", m => " " + m.Value.ToUpperInvariant());
                    this.name = char.ToUpperInvariant(output[0]) + output.Substring(1);
                }
                return name;
            }
        }

        public delegate void ActionEventHandler(object sender, ActionEventArgs e);

        public event ActionEventHandler? ActionEvent;

        public void SendActionEvent(ActionEventArgs e)
        {
            ActionEvent?.Invoke(this, e);
        }

        public void SetState(Dictionary<GoapKey, bool> newState)
        {
            State = newState;
        }

        public virtual bool CheckIfActionCanRun()
        {
            return true;
        }

        public virtual ValueTask OnEnter()
        {
            return ValueTask.CompletedTask;
        }

        public virtual ValueTask OnExit()
        {
            return ValueTask.CompletedTask;
        }

        public abstract ValueTask PerformAction();

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

        public virtual void OnActionEvent(object sender, ActionEventArgs e)
        {
        }

        public virtual string Description()
        {
            return $"{Name} " + (Keys.Count == 1 ? $"[{Keys[0].ConsoleKey}]" : "");
        }
    }
}