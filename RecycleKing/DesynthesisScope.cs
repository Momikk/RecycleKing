namespace RecycleKing;

internal readonly record struct DesynthItem(int Inventory, int Slot, uint Id, int Quantity, byte Flags, string State, string Name);
internal readonly record struct DesynthRow(int Inventory, int Slot, uint Quantity);

/// <summary>The full native tab captured by one explicit click, reduced only by acknowledged actions.</summary>
internal sealed class DesynthesisScope
{
    private readonly List<DesynthItem> remaining;
    internal int Category { get; }
    internal bool Empty => remaining.Count == 0;
    internal DesynthItem Head => remaining[0];
    internal IReadOnlyList<DesynthItem> Remaining => remaining;

    internal DesynthesisScope(int category, IEnumerable<DesynthItem> items)
    {
        Category = category;
        remaining = items.ToList();
        if (category is < 0 or > 7 || remaining.Count > 140
            || remaining.Any(x => x.Id is 0 or >= 500_000 || x.Quantity <= 0 || x.Slot < 0)
            || remaining.Select(x => (x.Inventory, x.Slot)).Distinct().Count() != remaining.Count)
            throw new ArgumentException("Invalid or ambiguous native desynthesis list.");
    }

    internal bool Matches(int agentCategory, int addonCategory, IReadOnlyList<DesynthItem> listed)
        => agentCategory == Category && addonCategory == Category && remaining.SequenceEqual(listed);

    internal static bool MatchesRow(DesynthRow ui, DesynthRow agent, DesynthItem item)
        => ui == agent && ui.Inventory == item.Inventory && ui.Slot == item.Slot
            && ui.Quantity > 0 && ui.Quantity == item.Quantity;

    internal static bool SameIdentity(DesynthItem a, DesynthItem b)
        => a.Inventory == b.Inventory && a.Slot == b.Slot && a.Id == b.Id
            && a.Flags == b.Flags && a.State == b.State;

    internal static bool OwnSelection(DesynthItem expected, DesynthItem selected)
        => SameIdentity(expected, selected) && selected.Quantity > 0 && selected.Quantity <= expected.Quantity;

    // Only call after the native result identifies the submitted item. An output
    // material may already occupy the consumed item's former slot.
    internal bool ConsumeOne(uint resultItemId, DesynthItem? after)
    {
        if (Empty || RecycleList.BaseId(resultItemId) != Head.Id) return false;
        var before = Head;
        if (before.Quantity == 1)
        {
            if (after is { } value && value.Id == before.Id) return false;
            remaining.RemoveAt(0);
            return true;
        }
        if (after is not { } current || !SameIdentity(before, current) || current.Quantity != before.Quantity - 1)
            return false;
        remaining[0] = before with { Quantity = before.Quantity - 1 };
        return true;
    }
}
