using Core.GOAP;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Core.Goals
{
    public class GoapPreCondition
    {
        public string Description { get; }
        public object State { get; }

        public GoapPreCondition(string description, object state)
        {
            this.Description = description;
            this.State = state;
        }
    }

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
        public HashSet<KeyValuePair<GoapKey, GoapPreCondition>> Preconditions { get; } = new();
        public HashSet<KeyValuePair<GoapKey, bool>> Effects { get; } = new();

        public List<KeyAction> Keys { get; } = new();

        public Dictionary<string, bool> State { get; private set; } = new();

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

        public void SetState(Dictionary<string, bool> newState)
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

        public void AddPrecondition(GoapKey key, object value)
        {
            var precondition = new GoapPreCondition(GoapKeyDescription.ToString(key, value), value);
            Preconditions.Add(new KeyValuePair<GoapKey, GoapPreCondition>(key, precondition));
        }

        public void RemovePrecondition(GoapKey key)
        {
            Preconditions.RemoveWhere(o => o.Key.Equals(key));
        }

        public void AddEffect(GoapKey key, bool value)
        {
            Effects.Add(new(key, value));
        }

        public void RemoveEffect(GoapKey key)
        {
            Effects.RemoveWhere(o => o.Key.Equals(key));
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