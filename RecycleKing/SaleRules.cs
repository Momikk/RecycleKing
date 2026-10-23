namespace RecycleKing;

internal readonly record struct SaleSlot(int Page, int Slot, uint Id, int Quantity, byte Flags, string Fingerprint, uint UnitPrice, string Name);
internal readonly record struct SaleContext(byte Kind, nint Agent, nint Addon, uint AddonId, ulong Character, uint Territory, ulong Retainer);

internal static class SaleRules
{
    internal static bool RetainerAvailable(bool agentActive, ulong liveId, ulong selectedId)
        => agentActive && liveId != 0 && liveId == selectedId;

    internal static bool RetainerSellAction(ulong callback) => callback == 5;

    internal const ulong GilCap = 999_999_999;

    internal static bool Eligible(uint id, int quantity, uint price, bool symbolic, IReadOnlySet<uint> list)
        => !symbolic && id is > 0 and < 500_000 && quantity > 0 && price > 0 && list.Contains(id);

    internal static bool SameSlot(SaleSlot expected, SaleSlot current) => expected == current;

    internal static bool OwnTransaction(SaleSlot expected, int type, uint itemId, int inventory, int slot)
        => type == 2 && itemId == expected.Id && inventory == expected.Page && slot == expected.Slot;

    internal static bool Confirmed(SaleSlot expected, long totalBefore, long totalAfter, ulong gilBefore, ulong gilAfter)
        => expected.Quantity > 0 && totalBefore - totalAfter == expected.Quantity && gilAfter > gilBefore
            && gilAfter - gilBefore >= (ulong)expected.UnitPrice * (ulong)expected.Quantity;

    internal static bool CanFit(SaleSlot item, ulong gil)
        => gil <= GilCap && (ulong)item.UnitPrice * (ulong)item.Quantity <= GilCap - gil;

    internal static bool QuantityMatches(SaleSlot item, uint min, uint max)
        => min == 1 && item.Quantity > 0 && max == (uint)item.Quantity;
}
