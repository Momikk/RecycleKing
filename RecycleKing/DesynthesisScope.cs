namespace RecycleKing;

internal readonly record struct DesynthItem(int Inventory, int Slot, uint Id, int Quantity, byte Flags, string State, string Name);
internal readonly record struct DesynthRow(int Inventory, int Slot, uint Quantity);
internal readonly record struct DesynthListMatch(int HeadIndex, int OutputRows);

/// <summary>The full native tab captured by one explicit click, reduced only by acknowledged actions.</summary>
internal sealed class DesynthesisScope
{
    private readonly List<DesynthItem> remaining;
    private readonly Dictionary<(int Inventory, int Slot), DesynthItem> initialBags;
    private readonly HashSet<(int Inventory, int Slot)> capturedSlots;
    private readonly Dictionary<(int Inventory, int Slot), DesynthItem> verifiedOutputs = new();
    private readonly Dictionary<uint, long> receivedOutputs = new();
    internal int Category { get; }
    internal bool Empty => remaining.Count == 0;
    internal DesynthItem Head => remaining[0];
    internal IReadOnlyList<DesynthItem> Remaining => remaining;

    internal DesynthesisScope(int category, IEnumerable<DesynthItem> items, IEnumerable<DesynthItem>? initialBags = null)
    {
        Category = category;
        remaining = items.ToList();
        if (category is < 0 or > 7 || remaining.Count > 140
            || remaining.Any(x => x.Id is 0 or >= 500_000 || x.Quantity <= 0 || x.Slot < 0)
            || remaining.Select(x => (x.Inventory, x.Slot)).Distinct().Count() != remaining.Count)
            throw new ArgumentException("Invalid or ambiguous native desynthesis list.");
        capturedSlots = remaining.Select(item => (item.Inventory, item.Slot)).ToHashSet();
        this.initialBags = (initialBags ?? []).ToDictionary(item => (item.Inventory, item.Slot));
    }

    internal bool Matches(int agentCategory, int addonCategory, IReadOnlyList<DesynthItem> listed)
        => Match(agentCategory, addonCategory, listed) != null;

    internal DesynthListMatch? Match(int agentCategory, int addonCategory, IReadOnlyList<DesynthItem> listed)
    {
        if (agentCategory != Category || addonCategory != Category) return null;
        if (remaining.SequenceEqual(listed) && verifiedOutputs.Count == 0) return new(Empty ? -1 : 0, 0);
        if (listed.Count <= remaining.Count || receivedOutputs.Count == 0) return null;

        // The received materials can gain visible rows. Leave every such row out of
        // the plan and still require the original rows to agree in identity and order.
        var originalSlots = remaining.Select(item => (item.Inventory, item.Slot)).ToHashSet();
        var seenSlots = new HashSet<(int, int)>();
        var outputTotals = new Dictionary<uint, long>();
        var matchedOutputs = new Dictionary<(int, int), DesynthItem>();
        var next = 0;
        var headIndex = -1;
        for (var i = 0; i < listed.Count; i++)
        {
            var item = listed[i];
            var slot = (item.Inventory, item.Slot);
            if (!seenSlots.Add(slot)) return null;
            if (originalSlots.Contains(slot))
            {
                if (next == remaining.Count || item != remaining[next]) return null;
                if (next++ == 0) headIndex = i;
                continue;
            }

            // Outputs are received in normal bags. Pin a verified row to its exact
            // slot and state; a later move or quality substitution is not a receipt.
            if (item.Inventory is < 0 or > 3 || item.Slot is < 0 or >= 35 || item.Quantity <= 0
                || (verifiedOutputs.TryGetValue(slot, out var prior)
                    && (!SameIdentity(prior, item) || prior.Name != item.Name || item.Quantity < prior.Quantity)))
                return null;
            var baseline = 0;
            if (initialBags.TryGetValue(slot, out var initial))
            {
                if (SameIdentity(initial, item) && initial.Name == item.Name) baseline = initial.Quantity;
                else if (!capturedSlots.Contains(slot)) return null;
            }
            var added = (long)item.Quantity - baseline;
            var outputId = item.Id + ((item.Flags & 1) != 0 ? 1_000_000u : 0);
            var total = outputTotals.GetValueOrDefault(outputId) + added;
            if (added <= 0 || !receivedOutputs.TryGetValue(outputId, out var received) || total > received)
                return null;
            outputTotals[outputId] = total;
            matchedOutputs.Add(slot, item);
        }
        if (next != remaining.Count || verifiedOutputs.Keys.Any(slot => !matchedOutputs.ContainsKey(slot))) return null;
        // Commit bindings only after the entire original list and all receipts agree.
        foreach (var (slot, item) in matchedOutputs) verifiedOutputs[slot] = item;
        return new(headIndex, listed.Count - next);
    }

    // Called only for outputs of a native result whose source and one-unit consumption were verified.
    internal void RecordReceivedOutput(uint itemId, long quantity)
    {
        var id = RecycleList.BaseId(itemId);
        if (id is 0 or >= 500_000 || quantity <= 0 || quantity > int.MaxValue) return;
        // Keep the native HQ encoding; an NQ receipt cannot approve an HQ row.
        receivedOutputs[itemId] = receivedOutputs.GetValueOrDefault(itemId) + quantity;
    }

    internal static bool MatchesRow(DesynthRow ui, DesynthRow agent, DesynthItem item)
        => ui == agent && ui.Inventory == item.Inventory && ui.Slot == item.Slot
            && ui.Quantity > 0 && ui.Quantity == item.Quantity;

    internal static bool SameIdentity(DesynthItem a, DesynthItem b)
        => a.Inventory == b.Inventory && a.Slot == b.Slot && a.Id == b.Id
            && a.Flags == b.Flags && a.State == b.State;

    internal static bool OwnSelection(DesynthItem expected, DesynthItem selected)
        => SameIdentity(expected, selected) && selected.Quantity > 0 && selected.Quantity <= expected.Quantity;

    internal static bool OwnResult(uint expected, uint resultSource, uint agentSource)
        => expected is > 0 and < 500_000
            && RecycleList.BaseId(resultSource) == expected && RecycleList.BaseId(agentSource) == expected;

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
