using FFXIVClientStructs.FFXIV.Client.Game;
using Item = Lumina.Excel.Sheets.Item;
namespace RecycleKing;

internal static unsafe class NativeInventory
{
    internal static readonly InventoryType[] ArmouryTypes =
    [
        InventoryType.ArmoryMainHand, InventoryType.ArmoryOffHand, InventoryType.ArmoryHead,
        InventoryType.ArmoryBody, InventoryType.ArmoryHands, InventoryType.ArmoryLegs, InventoryType.ArmoryFeets,
        InventoryType.ArmoryEar, InventoryType.ArmoryNeck, InventoryType.ArmoryWrist, InventoryType.ArmoryRings,
        InventoryType.ArmorySoulCrystal,
    ];
    internal static DesynthItem? ReadSlot(int inventory, int slot)
        => TryReadSlot(inventory, slot, out var value) ? value : null;

    internal static bool TryReadSlot(int inventory, int slot, out DesynthItem? value)
    {
        value = null;
        if (!AllowedInventory((InventoryType)inventory) || slot < 0) return false;
        var manager = InventoryManager.Instance();
        var container = manager == null ? null : manager->GetInventoryContainer((InventoryType)inventory);
        if (container == null || !container->IsLoaded || container->Type != (InventoryType)inventory
            || container->Size is < 1 or > 140 || container->Items == null || slot >= container->Size) return false;
        var item = container->GetInventorySlot(slot);
        if (item == null || (int)item->Container != inventory || item->Slot != slot) return false;
        if (item->ItemId == 0) return true;
        value = ReadItem(item);
        return value != null;
    }

    internal static bool AllowedInventory(InventoryType type) => type is InventoryType.Inventory1 or InventoryType.Inventory2
        or InventoryType.Inventory3 or InventoryType.Inventory4 or InventoryType.EquippedItems
        or InventoryType.ArmoryMainHand or InventoryType.ArmoryOffHand or InventoryType.ArmoryHead or InventoryType.ArmoryBody
        or InventoryType.ArmoryHands or InventoryType.ArmoryLegs or InventoryType.ArmoryFeets or InventoryType.ArmoryEar
        or InventoryType.ArmoryNeck or InventoryType.ArmoryWrist or InventoryType.ArmoryRings or InventoryType.ArmorySoulCrystal;

    internal static DesynthItem? ReadItem(InventoryItem* item)
    {
        if (item == null || item->IsSymbolic || item->ItemId is 0 or >= 500_000 || item->Quantity <= 0
            || !AllowedInventory(item->Container) || !Plugin.Data.GetExcelSheet<Item>().TryGetRow(item->ItemId, out var row)) return null;
        var state = $"{item->Condition}:{item->SpiritbondOrCollectability}:{item->CrafterContentId}:{item->GlamourId}:{item->EventId}";
        for (var i = 0; i < 5; i++) state += $":{item->Materia[i]}/{item->MateriaGrades[i]}";
        for (var i = 0; i < 2; i++) state += $":{item->Stains[i]}";
        return new DesynthItem((int)item->Container, item->Slot, item->ItemId, item->Quantity, (byte)item->Flags, state, row.Name.ToString());
    }

}
