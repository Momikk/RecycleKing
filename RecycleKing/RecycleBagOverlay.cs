using System.Numerics;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Bounds = FFXIVClientStructs.FFXIV.Common.Math.Bounds;

namespace RecycleKing;

/// <summary>Read-only inventory mapping and non-interactive draw commands; never changes slot colours.</summary>
internal sealed unsafe class RecycleBagOverlay(Plugin plugin)
{
    private static readonly string[] CoverAddons =
    ["ItemDetail", "ActionDetail", "Tooltip", "ContextMenu", "AddonContextSub", "AddonContextMenuTitle",
        "SelectYesno", "InputNumeric", "RetainerSell"];
    private HashSet<uint> targets = [];
    private int revision = -1;
    private long retryAfter;

    internal void Draw()
    {
        if (Environment.TickCount64 < retryAfter) return;
        try { DrawItems(); }
        catch (Exception exception)
        {
            retryAfter = Environment.TickCount64 + 5000;
            Plugin.Log.Error(exception, "回收之王无法绘制背包标记。");
        }
    }

    private static bool Visible(string name)
    {
        var addon = Plugin.GameGui.GetAddonByName(name);
        return !addon.IsNull && addon.IsReady && addon.IsVisible;
    }

    private void DrawItems()
    {
        if (!plugin.Config.HighlightListedItems || !Plugin.Player.IsLoaded) return;
        if (revision != plugin.Revision)
        {
            targets = plugin.HighlightedItemIds().ToHashSet();
            revision = plugin.Revision;
        }
        if (targets.Count == 0) return;
        var expanded = Visible("InventoryExpansion");
        if (!expanded && !Visible("Inventory") && !Visible("InventoryLarge")) return;
        var order = ItemOrderModule.Instance();
        var sorter = order == null ? null : order->InventorySorter;
        var manager = InventoryManager.Instance();
        if (manager == null || sorter == null || sorter->SortFunctionIndex != -1 || sorter->InventoryType != InventoryType.Inventory1
            || sorter->ItemsPerPage != 35 || sorter->Items.LongCount != 140) return;

        var draw = ImGui.GetForegroundDrawList();
        var origin = ImGui.GetMainViewport().Pos;
        var covers = ReadCovers(origin);
        if (covers == null) return;
        var fill = ImGui.ColorConvertFloat4ToU32(RecycleAppearance.Pink(.28f));
        var pink = ImGui.ColorConvertFloat4ToU32(RecycleAppearance.Pink(1));
        var shadow = ImGui.ColorConvertFloat4ToU32(new Vector4(.08f, .01f, .03f, .95f));
        for (var page = 0; page < 4; page++)
        {
            var handle = Plugin.GameGui.GetAddonByName($"InventoryGrid{page}{(expanded ? "E" : "")}");
            if (handle.IsNull || !handle.IsReady || !handle.IsVisible) continue;
            var grid = (AddonInventoryGrid*)handle.Address;
            for (var index = 0; index < 35; index++)
            {
                var source = sorter->Items[page * 35 + index].Value;
                if (source == null || !RecycleAppearance.ValidSource(source->Page, source->Slot)) continue;
                var bag = manager->GetInventoryContainer((InventoryType)((int)InventoryType.Inventory1 + source->Page));
                if (bag == null || !bag->IsLoaded || bag->Items == null || bag->Size != 35) continue;
                var item = bag->Items[source->Slot];
                if (!RecycleAppearance.Matches(targets, item.ItemId, item.Quantity)) continue;
                var slot = grid->Slots[index].Value;
                if (slot == null || slot->OwnerNode == null) continue;
                var node = (AtkResNode*)slot->OwnerNode;
                if (!NodeVisible(node)) continue;
                var bounds = new Bounds();
                node->GetBounds(&bounds);
                if (bounds.Width is < 12 or > 200 || bounds.Height is < 12 or > 200) continue;
                var min = origin + new Vector2(bounds.Pos1.X, bounds.Pos1.Y) + Vector2.One;
                var max = origin + new Vector2(bounds.Pos2.X, bounds.Pos2.Y) - Vector2.One;
                if (OverlayOcclusion.Covered(new(min, max), covers)) continue;
                draw.AddRectFilled(min, max, fill, 3);
                draw.AddRect(min, max, pink, 3, ImDrawFlags.None, 1.5f);
                var inset = (max - min) * .24f;
                var a = min + inset;
                var b = max - inset;
                var c = new Vector2(a.X, b.Y);
                var d = new Vector2(b.X, a.Y);
                var thickness = Math.Clamp((max.X - min.X) / 18, 1.5f, 5);
                draw.AddLine(a, b, shadow, thickness + 2);
                draw.AddLine(c, d, shadow, thickness + 2);
                draw.AddLine(a, b, pink, thickness);
                draw.AddLine(c, d, pink, thickness);
            }
        }
    }

    private static List<OverlayBounds>? ReadCovers(Vector2 origin)
    {
        var covers = new List<OverlayBounds>();
        foreach (var name in CoverAddons)
        {
            var handle = Plugin.GameGui.GetAddonByName(name);
            if (handle.IsNull || !handle.IsReady || !handle.IsVisible) continue;
            var addon = (AtkUnitBase*)handle.Address;
            var root = addon->RootNode;
            // While an open popup is rebuilding, omit this frame rather than paint over it.
            if (root == null) return null;
            if (!NodeVisible(root)) continue;
            var bounds = new Bounds();
            root->GetBounds(&bounds);
            var cover = new OverlayBounds(origin + new Vector2(bounds.Pos1.X, bounds.Pos1.Y),
                origin + new Vector2(bounds.Pos2.X, bounds.Pos2.Y));
            if (!cover.Valid) return null;
            covers.Add(cover);
        }
        return covers;
    }

    private static bool NodeVisible(AtkResNode* node)
    {
        for (var depth = 0; node != null && depth < 32; depth++, node = node->ParentNode)
            if (!node->IsVisible()) return false;
        return node == null;
    }
}
