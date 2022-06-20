namespace Core
{
    public class ActionBarPopulator
    {
        private readonly struct ActionBarSlotItem
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

        private int count;

        public ActionBarPopulator(ClassConfiguration config, BagReader bagReader, ExecGameCommand execGameCommand)
        {
            this.config = config;
            this.bagReader = bagReader;
            this.execGameCommand = execGameCommand;
        }

        public void Execute()
        {
            ActionBarSlotItem[] items = new ActionBarSlotItem[
                config.Form.Length +
                config.Adhoc.Sequence.Length +
                config.Parallel.Sequence.Length +
                config.Pull.Sequence.Length +
                config.Combat.Sequence.Length +
                config.NPC.Sequence.Length];

            foreach (var k in config.Form)
            {
                AddUnique(ref items, k);
            }

            foreach (var k in config.Adhoc.Sequence)
            {
                AddUnique(ref items, k);
            }

            foreach (var k in config.Parallel.Sequence)
            {
                AddUnique(ref items, k);
            }

            foreach (var k in config.Pull.Sequence)
            {
                AddUnique(ref items, k);
            }

            foreach (var k in config.Combat.Sequence)
            {
                AddUnique(ref items, k);
            }

            foreach (var k in config.NPC.Sequence)
            {
                AddUnique(ref items, k);
            }

            System.Array.Resize(ref items, count);
            System.Array.Sort(items, (a, b) => a.KeyAction.Slot.CompareTo(b.KeyAction.Slot));

            for (int i = 0; i < count; i++)
            {
                string content = ScriptBuilder(items[i]);
                execGameCommand.Run(content);
            }
        }

        private void AddUnique(ref ActionBarSlotItem[] items, KeyAction keyAction)
        {
            // not bound to actionbar slot
            if (keyAction.Slot == 0) return;

            for (int i = 0; i < count; i++)
            {
                if (items[i].KeyAction.Slot == keyAction.Slot)
                    return;
            }

            string name = keyAction.Name;
            bool isItem = false;
            switch (name)
            {
                case "water":
                case "Water":
                    name = bagReader.HighestQuantityOfWaterId().ToString();
                    isItem = true;
                    break;
                case "food":
                case "Food":
                    name = bagReader.HighestQuantityOfFoodId().ToString();
                    isItem = true;
                    break;
            }

            items[count++] = (new(name, keyAction, isItem));
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
