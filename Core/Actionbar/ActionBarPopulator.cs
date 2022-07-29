using System.Collections.Generic;

namespace Core
{
    public class ActionBarPopulator
    {
        private class ActionBarSlotItem
        {
            public string Name { get; }
            public KeyAction KeyAction { get; }
            public bool IsItem { get; }

            public ActionBarSlotItem(string name, KeyAction keyAction, bool isItem)
            {
                Name = name;
                KeyAction = keyAction;
                IsItem = isItem;
            }
        }

        private readonly ClassConfiguration config;
        private readonly BagReader bagReader;
        private readonly ExecGameCommand execGameCommand;

        public ActionBarPopulator(ClassConfiguration config, BagReader bagReader, ExecGameCommand execGameCommand)
        {
            this.config = config;
            this.bagReader = bagReader;
            this.execGameCommand = execGameCommand;
        }

        public void Execute()
        {
            List<ActionBarSlotItem> items = new();

            for (int i = 0; i < config.Form.Length; i++)
            {
                AddUnique(items, config.Form[i]);
            }

            for (int i = 0; i < config.Adhoc.Sequence.Length; i++)
            {
                AddUnique(items, config.Adhoc.Sequence[i]);
            }

            for (int i = 0; i < config.Parallel.Sequence.Length; i++)
            {
                AddUnique(items, config.Parallel.Sequence[i]);
            }

            for (int i = 0; i < config.Pull.Sequence.Length; i++)
            {
                AddUnique(items, config.Pull.Sequence[i]);
            }

            for (int i = 0; i < config.Combat.Sequence.Length; i++)
            {
                AddUnique(items, config.Combat.Sequence[i]);
            }

            for (int i = 0; i < config.NPC.Sequence.Length; i++)
            {
                AddUnique(items, config.NPC.Sequence[i]);
            }

            items.Sort((a, b) => a.KeyAction.Slot.CompareTo(b.KeyAction.Slot));

            for (int i = 0; i < items.Count; i++)
            {
                string content = ScriptBuilder(items[i]);
                execGameCommand.Run(content);
            }
        }

        private void AddUnique(List<ActionBarSlotItem> items, KeyAction keyAction)
        {
            // not bound to actionbar slot
            if (keyAction.Slot == 0) return;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].KeyAction.Slot == keyAction.Slot)
                    return;
            }

            string name = keyAction.Name;
            bool isItem = false;

            if (name.Equals(RequirementFactory.Drink, System.StringComparison.OrdinalIgnoreCase))
            {
                name = bagReader.HighestQuantityOfDrinkId().ToString();
                isItem = true;
            }
            else if (name.Equals(RequirementFactory.Food, System.StringComparison.OrdinalIgnoreCase))
            {
                name = bagReader.HighestQuantityOfFoodId().ToString();
                isItem = true;
            }

            items.Add(new(name, keyAction, isItem));
        }

        private static string ScriptBuilder(ActionBarSlotItem abs)
        {
            string nameOrId = $"\"{abs.Name}\"";
            if (int.TryParse(abs.Name, out int id))
            {
                nameOrId = id.ToString();
            }

            string func = GetFunction(abs);
            int slot = abs.KeyAction.Slot;
            return $"/run {func}({nameOrId})PlaceAction({slot})ClearCursor()--";
        }

        private static string GetFunction(ActionBarSlotItem a)
        {
            if (a.IsItem)
                return "PickupItem";

            if (char.IsLower(a.Name[0]))
                return "PickupMacro";

            return "PickupSpellBookItem";
        }
    }
}
