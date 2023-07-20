using System.Collections.Specialized;

using static Core.ActionBar;

namespace Core;

public interface IActionBarBits
{
    void Update(IAddonDataProvider reader);
    bool Is(KeyAction keyAction);
}

public interface ICurrentAction : IActionBarBits { }

public interface IUsableAction : IActionBarBits { }

public sealed class ActionBarBits<T> : IActionBarBits, IReader
{
    private readonly int[] cells;

    private readonly BitVector32[] bits;

    public ActionBarBits(params int[] cells)
    {
        this.cells = cells;
        bits = new BitVector32[cells.Length];
    }

    public void Update(IAddonDataProvider reader)
    {
        for (int i = 0; i < bits.Length; i++)
        {
            bits[i] = new(reader.GetInt(cells[i]));
        }
    }

    // https://wowwiki-archive.fandom.com/wiki/ActionSlot
    public bool Is(KeyAction keyAction)
    {
        if (keyAction.Slot == 0) return false;

        int index = keyAction.SlotIndex;
        return bits
            [index / BIT_PER_CELL]
            [Mask.M[index % BIT_PER_CELL]];
    }
}
