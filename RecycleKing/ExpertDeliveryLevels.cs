using System.Globalization;
using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using Lumina.Data.Parsing.Uld;
using Item = Lumina.Excel.Sheets.Item;

namespace RecycleKing;

/// <summary>Decorates visible native rows only. Never changes the delivery selection or submits items.</summary>
internal sealed unsafe class ExpertDeliveryLevels : IDisposable
{
    private const string AddonName = "GrandCompanySupplyList";
    private sealed record Decoration(TextNode Label, nint Name, ushort Width, ushort AppliedWidth);
    private readonly Dictionary<nint, Decoration> rows = [];
    private TextNode? heading;
    private long retryAfter;

    internal ExpertDeliveryLevels()
    {
        Plugin.Addons.RegisterListener(AddonEvent.PreDraw, AddonName, Draw);
        Plugin.Addons.RegisterListener(AddonEvent.PreFinalize, AddonName, FinalizeAddon);
    }

    private void Draw(AddonEvent type, AddonArgs args)
    {
        if (Environment.TickCount64 < retryAfter) return;
        try { Refresh((AddonGrandCompanySupplyList*)args.Addon.Address); }
        catch (Exception exception)
        {
            HideAll();
            retryAfter = Environment.TickCount64 + 5000;
            Plugin.Log.Error(exception, "回收之王无法显示稀有品品级。");
        }
    }

    private void HideAll()
    {
        if (heading is not null) heading.IsVisible = false;
        foreach (var row in rows.Values) row.Label.IsVisible = false;
    }

    private void Refresh(AddonGrandCompanySupplyList* addon)
    {
        HideAll();
        if (addon == null || addon->SelectedTab != 2 || addon->ExpertDeliveryList == null) return;
        var agent = AgentGrandCompanySupply.Instance();
        if (agent == null || agent->SelectedTab != 2 || agent->ItemArray == null || agent->NumItems is < 0 or > 4096) return;
        var list = addon->ExpertDeliveryList;
        if (list->ItemRendererList == null || list->ListLength is < 0 or > 4096) return;

        // Sorting/filtering can change visual row indices. Resolve by the row's actual item name;
        // ambiguous names with different levels are deliberately omitted.
        var candidates = new List<(string Name, uint Level)>();
        var sheet = Plugin.Data.GetExcelSheet<Item>();
        for (var i = 0; i < agent->NumItems; i++)
        {
            var item = agent->ItemArray[i];
            if (item.Position < 11) continue;
            if (sheet.TryGetRow(RecycleList.BaseId(item.ItemId), out var data))
                candidates.Add((data.Name.ExtractText(), data.LevelItem.RowId));
        }
        var levels = DeliveryLevelMatch.UniqueLevels(candidates);
        var visited = new HashSet<nint>();
        for (var i = 0; i < Math.Min(list->ListLength, list->AllocatedItemRendererListLength); i++)
        {
            var renderer = list->ItemRendererList[i].AtkComponentListItemRenderer;
            if (renderer == null || !visited.Add((nint)renderer) || renderer->ListItemIndex < 0) continue;
            var owner = renderer->OwnerNode;
            if (owner == null || !owner->AtkResNode.IsVisible()) continue;
            var uld = &renderer->UldManager;
            if (uld->NodeList == null || uld->NodeListCount > 100) continue;
            AtkTextNode* name = null;
            AtkTextNode* reward = null;
            uint level = 0;
            for (var n = 0; n < uld->NodeListCount; n++)
            {
                var node = uld->NodeList[n];
                if (node == null || node->Type != NodeType.Text || node->NodeId >= 100_000_000) continue;
                var text = (AtkTextNode*)node;
                var plain = text->NodeText.StringPtr.AsDalamudSeString().TextValue.Trim();
                if (DeliveryLevelMatch.TryGet(levels, plain, out var matched)) { name = text; level = matched; }
                else if (uint.TryParse(plain.Replace(",", ""), out var seals) && seals > 0) reward = text;
            }
            if (name == null || reward == null || name->ParentNode != reward->ParentNode) continue;
            var x = reward->X - 80;
            if (x < name->X + 140) continue;
            if (!rows.TryGetValue((nint)renderer, out var decoration))
            {
                var label = new TextNode
                {
                    FontSize = name->FontSize, Size = new Vector2(64, name->Height),
                    AlignmentType = AlignmentType.Right, TextColorID = 8,
                };
                try { label.AttachNode(name->ParentNode); }
                catch { label.Dispose(); throw; }
                var width = (ushort)Math.Max(1, x - name->X - 8);
                decoration = new(label, (nint)name, name->Width, width);
                rows.Add((nint)renderer, decoration);
            }
            if (decoration.Name != (nint)name) continue;
            name->Width = decoration.AppliedWidth;
            decoration.Label.Position = new(x, name->Y);
            decoration.Label.String = level.ToString(CultureInfo.InvariantCulture);
            decoration.Label.IsVisible = true;

            if (heading is null)
            {
                heading = new TextNode
                {
                    String = "品级", FontSize = 12, Size = new(64, 24), AlignmentType = AlignmentType.Right,
                    TextColorID = 8,
                };
                heading.AttachNode((AtkUnitBase*)addon);
            }
            // Map the row-local column into addon-local coordinates. All nodes inherit UI scale.
            var headerX = x;
            for (var parent = name->ParentNode; parent != null && parent != addon->RootNode; parent = parent->ParentNode)
                headerX += parent->X;
            heading.Position = new(headerX, 122);
            heading.IsVisible = true;
        }
        // Restore text widths in live renderers which stopped displaying a resolved item.
        RestoreHiddenNames(addon);
    }

    private void RestoreHiddenNames(AddonGrandCompanySupplyList* addon)
    {
        var list = addon->ExpertDeliveryList;
        if (list == null || list->ItemRendererList == null || list->ListLength is < 0 or > 4096) return;
        for (var i = 0; i < Math.Min(list->ListLength, list->AllocatedItemRendererListLength); i++)
        {
            var renderer = list->ItemRendererList[i].AtkComponentListItemRenderer;
            if (renderer == null || !rows.TryGetValue((nint)renderer, out var row) || row.Label.IsVisible) continue;
            // Reacquire through the current tree; do not dereference a cached native name pointer.
            var uld = &renderer->UldManager;
            if (uld->NodeList == null || uld->NodeListCount > 100) continue;
            for (var n = 0; n < uld->NodeListCount; n++)
            {
                var node = uld->NodeList[n];
                if ((nint)node == row.Name && node != null && node->Width == row.AppliedWidth) node->Width = row.Width;
            }
        }
    }

    private void Cleanup(AddonGrandCompanySupplyList* addon)
    {
        HideAll();
        if (addon != null) RestoreHiddenNames(addon);
        foreach (var row in rows.Values) row.Label.Dispose();
        rows.Clear();
        heading?.Dispose(); heading = null;
    }

    private void FinalizeAddon(AddonEvent type, AddonArgs args) => Cleanup((AddonGrandCompanySupplyList*)args.Addon.Address);

    public void Dispose()
    {
        Plugin.Addons.UnregisterListener(AddonEvent.PreDraw, AddonName, Draw);
        Plugin.Addons.UnregisterListener(AddonEvent.PreFinalize, AddonName, FinalizeAddon);
        var addon = Plugin.GameGui.GetAddonByName(AddonName);
        Cleanup(addon.IsNull ? null : (AddonGrandCompanySupplyList*)addon.Address);
    }
}
