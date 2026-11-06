namespace RecycleKing;

internal static class ArmouryMoveRules
{
    internal const int ReservedBagSlots = 5;

    internal static bool HasBagRoom(int emptySlots)
        => emptySlots > ReservedBagSlots && emptySlots <= 140;

    // Gearsets store HQ as base ID + 1,000,000. Protect every matching copy.
    internal static bool Protected(IReadOnlySet<uint> registered, DesynthItem item)
        => registered.Contains(item.Id + ((item.Flags & 1) != 0 ? 1_000_000u : 0));

    internal static bool Eligible(IReadOnlySet<uint> registered, DesynthItem item)
        => item.Id is > 0 and < 500_000 && item.Quantity == 1 && !Protected(registered, item);

    internal static bool Arrived(DesynthItem expected, DesynthItem actual)
        => expected.Id == actual.Id && expected.Quantity == actual.Quantity
            && expected.Flags == actual.Flags && expected.State == actual.State;
}
