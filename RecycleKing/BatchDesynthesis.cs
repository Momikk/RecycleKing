using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using Item = Lumina.Excel.Sheets.Item;

namespace RecycleKing;

/// <summary>A finite, ordered native-tab plan; never enables the game's same-item bulk mode.</summary>
internal sealed unsafe class BatchDesynthesis : IDisposable
{
    private const string SelectorName = "SalvageItemSelector";
    private readonly Plugin plugin;
    private TextButtonNode? button;
    private nint selectorAddress;
    private ushort selectorId;
    private nint agentAddress;
    private DesynthesisScope? scope;
    private ulong character;
    private uint territory;
    private long deadline;
    private long retryAfter;
    private int completed;
    private Phase phase;
    private bool ticking;
    private enum Phase { Ready, Opening, Submitted, Closing, Refreshing }
    internal bool IsRunning => scope != null;
    private string status = "按当前标签页列表顺序逐件分解，包含 HQ；再次点击可停止。";

    internal BatchDesynthesis(Plugin plugin)
    {
        this.plugin = plugin;
        Plugin.Addons.RegisterListener(AddonEvent.PreFinalize, SelectorName, FinalizeSelector);
        Plugin.Framework.Update += Update;
    }

    private static AtkUnitBase* Addon(string name, bool visible = true)
    {
        var handle = Plugin.GameGui.GetAddonByName(name);
        return handle.IsNull || !handle.IsReady || (visible && !handle.IsVisible)
            ? null : (AtkUnitBase*)handle.Address;
    }

    private void Update(IFramework framework)
    {
        if (ticking || Environment.TickCount64 < retryAfter) return;
        ticking = true;
        try
        {
            var selector = (AddonSalvageItemSelector*)Addon(SelectorName, false);
            if (button != null && (selector == null || (nint)selector != selectorAddress || selector->Id != selectorId))
            {
                // A missing PreFinalize must never let us dereference a stale attached node.
                Stop("分解窗口已变化，已停止。");
                return;
            }
            if (selector != null && selector->RootNode != null)
            {
                if (button == null && selector->IsVisible)
                {
                    selectorAddress = (nint)selector;
                    selectorId = selector->Id;
                    button = new TextButtonNode { String = "自动分解", Size = new Vector2(176, 28), OnClick = Toggle };
                    try { button.AttachNode((AtkUnitBase*)selector); }
                    catch { button.Dispose(); button = null; throw; }
                }
                if (button != null)
                {
                    button.Position = new Vector2(Math.Max(0, selector->RootNode->Width - 188), 48);
                    button.IsVisible = selector->IsVisible;
                    button.String = IsRunning ? "停止分解" : "自动分解";
                    button.IsEnabled = IsRunning || (!plugin.Sales.IsRunning && !HasConfirmation());
                    button.TextTooltip = status + "\n只处理点击时本页列出的物品，不切换标签；已提交的分解仍可能完成。";
                }
            }
            if (IsRunning) Advance(selector);
        }
        catch (Exception error)
        {
            Stop("分解状态无法核对，已停止，请查看日志。");
            Plugin.Log.Error(error, "回收之王自动分解已停止。");
            retryAfter = Environment.TickCount64 + 5000;
        }
        finally { ticking = false; }
    }

    private void Toggle()
    {
        if (IsRunning) { Stop("已停止；已提交的分解仍可能完成。"); return; }
        try
        {
            var selector = (AddonSalvageItemSelector*)Addon(SelectorName);
            var agent = AgentSalvage.Instance();
            if (selector == null || (nint)selector != selectorAddress || selector->Id != selectorId
                || agent == null || HasConfirmation() || plugin.Sales.IsRunning || !CharacterReady())
            { Notify("请先关闭当前确认或结果窗口，再开始自动分解。"); return; }
            if (!TryList(selector, agent, out var items)) { Notify("当前分解列表无法核对，未开始。"); return; }
            if (items.Count == 0) { Notify("当前标签页没有可分解物品。"); return; }
            var plan = new DesynthesisScope((int)selector->SelectedCategory, items);
            character = Plugin.Player.ContentId;
            territory = Plugin.ClientState.TerritoryType;
            agentAddress = (nint)agent;
            completed = 0;
            phase = Phase.Ready;
            deadline = Environment.TickCount64 + 10000;
            scope = plan;
            status = $"准备按序分解当前标签页的 {items.Count} 项物品。";
        }
        catch (Exception error)
        {
            Plugin.Log.Error(error, "回收之王无法开始自动分解。");
            Notify("当前列表状态不受支持，未开始自动分解。");
        }
    }

    private static bool CharacterReady() => Plugin.Player.IsLoaded && Plugin.Player.ContentId != 0
        && !Plugin.Conditions[ConditionFlag.BetweenAreas] && !Plugin.Conditions[ConditionFlag.BetweenAreas51]
        && !Plugin.Conditions[ConditionFlag.InCombat];

    private void Advance(AddonSalvageItemSelector* selector)
    {
        var plan = scope!;
        var agent = AgentSalvage.Instance();
        if (!CharacterReady() || Plugin.Player.ContentId != character || Plugin.ClientState.TerritoryType != territory
            || selector == null || (nint)selector != selectorAddress || selector->Id != selectorId
            || agent == null || (nint)agent != agentAddress
            || (int)selector->SelectedCategory != plan.Category || (int)agent->SelectedCategory != plan.Category)
        { Stop("角色、窗口或标签页已变化，已停止。"); return; }
        if (Addon("SelectYesno") != null || Addon("SalvageAutoDialog") != null || plugin.Sales.IsRunning)
        { Stop("出现其他操作或确认窗口，已停止。"); return; }
        if (Environment.TickCount64 >= deadline)
        { Stop(phase == Phase.Refreshing ? "当前列表与剩余计划不一致，已停止。" : "未能核对分解结果，已停止，不会重试该物品。"); return; }

        if (phase == Phase.Ready || phase == Phase.Refreshing)
        {
            if (HasConfirmation()) { Stop("出现非本次操作的分解窗口，已停止。"); return; }
            if (!selector->IsVisible) { Stop("分解列表已隐藏，已停止。"); return; }
            if (!TryList(selector, agent, out var listed) || !plan.Matches((int)agent->SelectedCategory, (int)selector->SelectedCategory, listed))
            {
                // The native list can lag the result. Wait without submitting anything.
                if (phase == Phase.Ready) Stop("当前列表或物品状态已变化，已停止。");
                return;
            }
            if (plan.Empty) { Stop("当前标签页已分解完成。"); return; }
            if (Plugin.Conditions[ConditionFlag.OccupiedInEvent]) return;
            if (!HasBagRoom()) { Stop("背包需要至少三个空格，已停止。"); return; }
            var current = plan.Head;
            if (!InventoryMatches(plan.Remaining, 0)) { Stop("待分解物品已变化，已停止。"); return; }
            // Submit the first row of the validated native tab, as a normal selection.
            // Event kind 0 / values 12,row is the selector's item-selection event.
            AtkValue result = default;
            var values = stackalloc AtkValue[2];
            values[0] = new AtkValue { Type = AtkValueType.Int, Int = 12 };
            values[1] = new AtkValue { Type = AtkValueType.Int, Int = 0 };
            phase = Phase.Opening;
            deadline = Environment.TickCount64 + 10000;
            status = $"正在分解：{current.Name}；已完成 {completed} 件。";
            agent->ReceiveEvent(&result, values, 2, 0);
            return;
        }

        if (phase == Phase.Closing)
        {
            if (!InventoryMatches(plan.Remaining, 0)) { Stop("待分解物品已变化，已停止。"); return; }
            if (Addon("SalvageResult") == null && !agent->IsSalvageResultAddonOpen)
            {
                phase = Phase.Refreshing;
                deadline = Environment.TickCount64 + 5000;
            }
            return;
        }
        if (!InventoryMatches(plan.Remaining, 1)) { Stop("其他待分解物品已变化，已停止。"); return; }
        var head = plan.Head;
        if (phase == Phase.Opening)
        {
            if (!InventoryMatches(plan.Remaining, 0)) { Stop("本次物品已变化，已停止。"); return; }
            var dialog = (AddonSalvageDialog*)Addon("SalvageDialog");
            if (dialog == null) return;
            var selected = ReadItem(&agent->DesynthItemSlot);
            if (selected == null || !DesynthesisScope.OwnSelection(head, selected.Value)
                || !TryList(selector, agent, out var listed) || !plan.Matches((int)agent->SelectedCategory, (int)selector->SelectedCategory, listed))
            { Stop("确认窗口与本次列表物品不一致，已停止。"); return; }
            // Inspect all typed checkbox slots; never assume a fixed checkbox index.
            // GuaranteeNQ is unrelated to bulk mode and is left as the user selected it.
            foreach (var check in dialog->Checkboxes)
            {
                if (check.Type == SalvageDialogCheckboxType.BulkDesynth && check.IsCheckedCached)
                { Stop("请取消原生整组分解选项后重试，避免处理列表外物品。"); return; }
                if (check.Checkbox == null) continue;
                if (check.Type is < SalvageDialogCheckboxType.BulkDesynth or > SalvageDialogCheckboxType.GuaranteeNQ)
                { Stop("分解确认选项无法识别，已停止。"); return; }
                if (check.Type == SalvageDialogCheckboxType.BulkDesynth && check.Checkbox->IsChecked)
                { Stop("请取消原生整组分解选项后重试，避免处理列表外物品。"); return; }
            }
            var nativeButton = dialog->DesynthesizeButton;
            if (!HasBagRoom()) { Stop("背包需要至少三个空格，已停止。"); return; }
            if (nativeButton == null || !nativeButton->IsEnabled)
            { Stop("当前物品需要手动处理分解警告，已停止。"); return; }
            phase = Phase.Submitted; // Set ownership before the synchronous native callback.
            deadline = Environment.TickCount64 + 20000;
            var args = stackalloc AtkValue[2];
            args[0] = new AtkValue { Type = AtkValueType.Int, Int = 0 };
            args[1] = new AtkValue { Type = AtkValueType.Int, Int = 0 };
            dialog->FireCallback(2, args, true);
            return;
        }

        if (phase == Phase.Submitted)
        {
            var result = Addon("SalvageResult");
            if (result == null || !agent->IsSalvageResultAddonOpen) return;
            if (RecycleList.BaseId(agent->DesynthItemId) != head.Id)
            { Stop("分解结果物品不一致，已停止。"); return; }
            if (!TryReadSlot(head.Inventory, head.Slot, out var after))
            { Stop("背包状态不可用，已停止。"); return; }
            if (after == head) return; // Acknowledgement can precede the inventory update.
            if (!plan.ConsumeOne(agent->DesynthItemId, after)) { Stop("本次物品数量变化不符，已停止。"); return; }
            completed++;
            phase = Phase.Closing;
            deadline = Environment.TickCount64 + 10000;
            // Dispatch the result window's own close-button event, not a guessed callback ID.
            var close = result->GetComponentButtonById(15);
            if (!ClickNativeButton(result, close)) Stop("无法关闭本次分解结果，已停止。");
            return;
        }

    }

    private static bool TryList(AddonSalvageItemSelector* selector, AgentSalvage* agent, out List<DesynthItem> items)
    {
        items = [];
        if (selector->SelectedCategory != agent->SelectedCategory || (int)selector->SelectedCategory is < 0 or > 7
            || selector->ItemCount > 140 || selector->ItemCount != agent->ItemCount
            || (agent->ItemCount > 0 && agent->ItemList == null)) return false;
        for (var i = 0; i < selector->ItemCount; i++)
        {
            var ui = selector->Items[i];
            var row = agent->ItemList[i];
            if (ui.Inventory != row.InventoryType || ui.Slot != row.InventorySlot || ui.Quantity != row.Quantity) return false;
            var value = ReadSlot((int)ui.Inventory, ui.Slot);
            if (value == null || value.Value.Quantity != ui.Quantity
                || !Plugin.Data.GetExcelSheet<Item>().TryGetRow(value.Value.Id, out var item) || item.Icon != ui.IconId) return false;
            // No CString/Utf8String pointer traversal; display names come from the item sheet.
            items.Add(value.Value);
        }
        return items.Select(x => (x.Inventory, x.Slot)).Distinct().Count() == items.Count;
    }

    private static DesynthItem? ReadSlot(int inventory, int slot)
        => TryReadSlot(inventory, slot, out var value) ? value : null;

    private static bool TryReadSlot(int inventory, int slot, out DesynthItem? value)
    {
        value = null;
        if (!AllowedInventory((InventoryType)inventory) || slot < 0) return false;
        var manager = InventoryManager.Instance();
        var container = manager == null ? null : manager->GetInventoryContainer((InventoryType)inventory);
        if (container == null || !container->IsLoaded || slot >= container->Size) return false;
        var item = container->GetInventorySlot(slot);
        if (item == null || (int)item->Container != inventory || item->Slot != slot) return false;
        if (item->ItemId == 0) return true;
        value = ReadItem(item);
        return value != null;
    }

    private static bool AllowedInventory(InventoryType type) => type is InventoryType.Inventory1 or InventoryType.Inventory2
        or InventoryType.Inventory3 or InventoryType.Inventory4 or InventoryType.EquippedItems
        or InventoryType.ArmoryMainHand or InventoryType.ArmoryOffHand or InventoryType.ArmoryHead or InventoryType.ArmoryBody
        or InventoryType.ArmoryHands or InventoryType.ArmoryLegs or InventoryType.ArmoryFeets or InventoryType.ArmoryEar
        or InventoryType.ArmoryNeck or InventoryType.ArmoryWrist or InventoryType.ArmoryRings;

    private static DesynthItem? ReadItem(InventoryItem* item)
    {
        if (item == null || item->IsSymbolic || item->ItemId is 0 or >= 500_000 || item->Quantity <= 0
            || !AllowedInventory(item->Container) || !Plugin.Data.GetExcelSheet<Item>().TryGetRow(item->ItemId, out var row)) return null;
        var state = $"{item->Condition}:{item->SpiritbondOrCollectability}:{item->CrafterContentId}:{item->GlamourId}:{item->EventId}";
        for (var i = 0; i < 5; i++) state += $":{item->Materia[i]}/{item->MateriaGrades[i]}";
        for (var i = 0; i < 2; i++) state += $":{item->Stains[i]}";
        return new DesynthItem((int)item->Container, item->Slot, item->ItemId, item->Quantity, (byte)item->Flags, state, row.Name.ToString());
    }

    private static bool InventoryMatches(IReadOnlyList<DesynthItem> items, int start)
    {
        for (var i = start; i < items.Count; i++)
            if (ReadSlot(items[i].Inventory, items[i].Slot) != items[i]) return false;
        return true;
    }

    private static bool HasBagRoom()
    {
        var manager = InventoryManager.Instance();
        if (manager == null) return false;
        var empty = 0;
        for (var page = 0; page < 4; page++)
        {
            var bag = manager->GetInventoryContainer((InventoryType)page);
            if (bag == null || !bag->IsLoaded) return false;
            for (var i = 0; i < bag->Size; i++)
            {
                var item = bag->GetInventorySlot(i);
                if (item != null && item->ItemId == 0) empty++;
            }
        }
        return empty >= 3;
    }

    private static bool ClickNativeButton(AtkUnitBase* addon, AtkComponentButton* button)
    {
        if (button == null || !button->IsEnabled || button->OwnerNode == null) return false;
        var node = &button->OwnerNode->AtkResNode;
        var evt = node->AtkEventManager.Event;
        // Bounded traversal, and only events owned by this exact addon/button.
        for (var i = 0; evt != null && i < 16; i++, evt = evt->NextEvent)
        {
            if (evt->Listener != (AtkEventListener*)addon || evt->State.EventType != AtkEventType.ButtonClick) continue;
            AtkEventData data = default;
            addon->ReceiveEvent(AtkEventType.ButtonClick, (int)evt->Param, evt, &data);
            return true;
        }
        return false;
    }

    private static bool HasConfirmation() => Addon("SalvageDialog") != null || Addon("SalvageResult") != null
        || Addon("SalvageAutoDialog") != null || Addon("SelectYesno") != null;

    private void Notify(string message)
    {
        status = message;
        Plugin.Chat.Print($"[回收之王] {message}");
    }

    private void Stop(string reason)
    {
        if (!IsRunning) return;
        scope = null;
        Notify($"{reason} 已确认分解 {completed} 件。");
    }

    private void FinalizeSelector(AddonEvent type, AddonArgs args)
    {
        if (args.Addon.Address != selectorAddress) return;
        Stop("分解窗口已关闭，已停止；已提交的分解仍可能完成。");
        button?.Dispose();
        button = null;
        selectorAddress = 0;
        selectorId = 0;
    }

    public void Dispose()
    {
        Stop("插件已关闭，已停止；已提交的分解仍可能完成。");
        Plugin.Framework.Update -= Update;
        Plugin.Addons.UnregisterListener(AddonEvent.PreFinalize, SelectorName, FinalizeSelector);
        button?.Dispose();
        button = null;
    }
}
