using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GameFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;
using AddonText = Lumina.Excel.Sheets.Addon;
using Item = Lumina.Excel.Sheets.Item;

namespace RecycleKing;

/// <summary>Runs only after a button click, through the game's own inventory menu and shop dialogs.</summary>
internal sealed unsafe class BatchSale : IDisposable
{
    private static readonly string[] ContextAddons = ["Shop", "InventoryRetainer", "InventoryRetainerLarge"];
    private static readonly string[] SaleAddons = ["ContextMenu", "InputNumeric", "SelectYesno"];
    private readonly BatchPump pump = new();
    private int progress;
    private readonly Plugin plugin;
    private readonly Queue<SaleSlot> queue = [];
    private readonly HashSet<string> confirmations = [];
    private HashSet<uint> list = [];
    private SaleContext context;
    private SaleSlot current;
    private int phase;
    private bool quantitySent;
    private long deadline;
    private long totalBefore;
    private ulong gilBefore;
    private int soldStacks;
    private long soldItems;
    private uint bagAddonId;

    internal bool IsRunning { get; private set; }
    internal string Status { get; private set; } = "点击废品回收，出售普通背包中的清单物品（含 HQ）。";
    internal bool CanStart => TryContext(out var value) && !Busy(value) && !HasDialog() && Addon("ContextMenu") == null;

    internal BatchSale(Plugin plugin)
    {
        this.plugin = plugin;
        Plugin.Framework.Update += Tick;
        Plugin.Addons.RegisterListener(AddonEvent.PreFinalize, ContextAddons, FinalizeContext);
        Plugin.Addons.RegisterListener(AddonEvent.PostSetup, SaleAddons, SaleAddonReady);
        Plugin.Addons.RegisterListener(AddonEvent.PostUpdate, SaleAddons, SaleAddonReady);
    }

    internal void Toggle()
    {
        if (IsRunning) { Stop("已停止；已提交的出售仍可能完成。"); return; }
        if (!CanStart || !TryContext(out context)) { Notify("当前无法出售物品。"); return; }
        list = plugin.Config.ItemIds.ToHashSet();
        queue.Clear();
        // A click captures a finite plan. Newly received or moved items are never silently added.
        if (!TryReadInventory(out var inventory)) { Notify("背包尚未加载完成。"); return; }
        foreach (var item in inventory)
            if (SaleRules.Eligible(item.Id, item.Quantity, item.UnitPrice, false, list)) queue.Enqueue(item);
        if (queue.Count == 0) { Notify("普通背包中没有可出售的清单物品。"); return; }
        IsRunning = true;
        soldStacks = 0;
        soldItems = 0;
        phase = 0;
        deadline = Environment.TickCount64 + 5000;
        Status = $"准备回收 {queue.Count} 组物品，再次点击可停止。";
    }

    private void Tick(IFramework framework) => Pump();

    private void SaleAddonReady(AddonEvent type, AddonArgs args)
    {
        if (args.AddonName == "ContextMenu" ? phase == 1 : phase == 2) Pump();
    }

    private void Pump()
    {
        if (!IsRunning) return;
        try { pump.Run(() => { if (IsRunning) Advance(); }, () => progress); }
        catch (Exception error)
        {
            Plugin.Log.Error(error, "回收之王批量出售已停止。");
            Stop("出售出现异常，已停止，请查看日志。");
        }
    }

    private void Advance()
    {
        if (!TryContext(out var active) || active != context) { Stop("出售窗口或角色状态已变化，已停止。"); return; }
        if (!list.SetEquals(plugin.Config.ItemIds)) { Stop("回收清单已修改，已停止。"); return; }
        if (!TryReadInventory(out var inventory)) { Stop("背包状态不可用，已停止。"); return; }
        var now = Environment.TickCount64;
        if (phase == 0)
        {
            if (queue.Count == 0) { Stop("回收完成。"); return; }
            if (HasDialog() || Addon("ContextMenu") != null) { Stop("出现其他交易或窗口，已停止。"); return; }
            if (Busy(context))
            {
                if (now >= deadline) Stop("交易状态持续繁忙，已停止。");
                return;
            }
            current = queue.Dequeue();
            if (!Unchanged(inventory)) { Stop("待出售物品的位置、数量或状态已变化，已停止。"); return; }
            gilBefore = Gil(context);
            if (!HasGilRoom(current, context)) { Stop("金币空间不足，已停止。"); return; }
            totalBefore = Total(inventory);
            confirmations.Clear();
            quantitySent = false;
            var bag = BagAddon();
            var agent = AgentInventoryContext.Instance();
            if (bag == null || agent == null) { Stop("背包或物品菜单不可用，已停止。"); return; }
            phase = 1;
            progress++;
            deadline = now + 5000;
            bagAddonId = bag->Id;
            agent->OpenForItemSlot((InventoryType)((int)InventoryType.Inventory1 + current.Page), current.Slot, 0, bag->Id);
            Status = $"正在回收：{current.Name} × {current.Quantity}";
            return;
        }
        if (now >= deadline) { Stop("未能确认本次出售结果，已停止，不会重试该物品。"); return; }
        if (phase == 1)
        {
            if (HasDialog()) { Stop("出现非本次出售的确认窗口，已停止。"); return; }
            var menu = (AddonContextMenu*)Addon("ContextMenu");
            if (menu == null) return;
            if (!Unchanged(inventory)) { Stop("待出售物品已变化，已停止。"); return; }
            if (!SelectSell(menu, out var reason, out var pending))
            {
                if (!pending) Stop($"无法核对原生出售菜单（{reason}），已停止。");
                return;
            }
            phase = 2;
            progress++;
            deadline = now + 15000;
            return;
        }

        var handler = Handler(context);
        var gil = Gil(context);
        if (SaleRules.Confirmed(current, totalBefore, Total(inventory), gilBefore, gil)
            && !HasDialog() && Addon("ContextMenu") == null && (handler == null || !handler->WaitingForTransactionToFinish))
        {
            soldStacks++;
            soldItems += current.Quantity;
            phase = 0;
            deadline = now + 5000;
            progress++;
            return;
        }
        if (handler == null || !OwnSale(handler))
        {
            if (HasDialog()) Stop("确认窗口与本次出售不匹配，已停止。");
            return;
        }
        var numeric = (AddonInputNumeric*)Addon("InputNumeric");
        if (numeric != null)
        {
            if (quantitySent) return;
            if (!Unchanged(inventory) || numeric->AtkValuesCount < 7
                || !SaleRules.QuantityMatches(current, numeric->AtkValues[2].UInt, numeric->AtkValues[3].UInt)
                || numeric->OkButton == null || !numeric->OkButton->IsEnabled)
            { Stop("出售数量窗口不匹配，已停止。"); return; }
            quantitySent = true;
            progress++;
            Callback((AtkUnitBase*)numeric, [current.Quantity]);
            return;
        }
        var confirm = (AddonSelectYesno*)Addon("SelectYesno");
        if (confirm != null)
        {
            var prompt = confirm->PromptText == null ? "" : confirm->PromptText->NodeText.StringPtr.AsDalamudSeString().TextValue;
            // Only accept a named-item prompt while the native shop explicitly awaits this exact sale.
            // Generic or unrelated prompts remain for the user, rather than clicking every Yes dialog.
            if (!handler->WaitingForSellConfirm || !Unchanged(inventory) || !prompt.Contains(current.Name, StringComparison.Ordinal)
                || confirm->YesButton == null || !confirm->YesButton->IsEnabled || confirmations.Count >= 3)
            { Stop("游戏确认窗口无法核对，已停止，请手动处理。"); return; }
            if (!confirmations.Add(prompt)) return;
            progress++;
            Callback((AtkUnitBase*)confirm, [0]);
        }
    }

    private bool SelectSell(AddonContextMenu* menu, out string reason, out bool pending)
    {
        pending = false;
        reason = "物品菜单来源不匹配";
        var agent = AgentInventoryContext.Instance();
        if (agent == null || agent->OwnerAddonId != bagAddonId
            || agent->TargetInventoryId != (InventoryType)((int)InventoryType.Inventory1 + current.Page)
            || agent->TargetInventorySlotId != current.Slot) return false;
        reason = "菜单内容不完整";
        if (menu->AtkValuesCount < 8) return false;
        var count = (int)menu->AtkValues[0].UInt;
        if (count is < 1 or > 32 || count + 8 > menu->AtkValuesCount) return false;

        // Inventory built-in entries do not require ContextCallbackInfos. That registry
        // is not indexed by visible menu rows, which may also include Dalamud entries.
        // Read the localized game label, then resolve its actual displayed row.
        string[] labels;
        if (context.Kind == 2)
        {
            reason = "雇员出售文本不可用";
            if (!Plugin.Data.GetExcelSheet<AddonText>().TryGetRow(5480, out var sell)) return false;
            var label = sell.Text.ExtractText();
            if (string.IsNullOrWhiteSpace(label)) return false;
            labels = [label];
        }
        else if (context.Kind == 1) labels = ["出售", "Sell", "売却", "Verkaufen", "Vendre"];
        else return false;

        var rows = new SaleMenuRow[count];
        var nativeList = menu->GetComponentListById(2);
        reason = "菜单列表尚未就绪";
        if (nativeList == null) { pending = true; return false; }
        for (var i = 0; i < count; i++)
        {
            var value = menu->AtkValues[8 + i];
            if (value.Type is not (AtkValueType.String or AtkValueType.ManagedString or AtkValueType.ConstString)) continue;
            var text = value.String.AsDalamudSeString();
            var renderer = nativeList->GetItemRenderer(i);
            if (renderer == null) { pending = true; return false; }
            rows[i] = new(text.TextValue, text.Payloads.Count == 1 && text.Payloads[0] is TextPayload,
                renderer != null && renderer->IsEnabled, (menu->AtkValues[3].UInt & (1u << i)) != 0);
        }
        var match = SaleRules.NativeSaleIndex(rows, labels);
        reason = "未找到唯一可用的原生出售项";
        if (match < 0) return false;
        Callback((AtkUnitBase*)menu, [0, match, 0]);
        return true;
    }

    private bool OwnSale(ShopEventHandler* handler) => SaleRules.OwnTransaction(current, handler->TransactionType,
        handler->TransactionItemId, (int)handler->SellInventoryType - (int)InventoryType.Inventory1, handler->SellInventorySlot);

    private bool Unchanged(List<SaleSlot> inventory) => inventory.Any(slot => SaleRules.SameSlot(current, slot));
    private long Total(List<SaleSlot> inventory) => inventory.Where(slot => slot.Id == current.Id && slot.Flags == current.Flags).Sum(slot => (long)slot.Quantity);

    private static bool TryReadInventory(out List<SaleSlot> result)
    {
        result = [];
        var manager = InventoryManager.Instance();
        if (manager == null) return false;
        var sheet = Plugin.Data.GetExcelSheet<Item>();
        for (var page = 0; page < 4; page++)
        {
            var bag = manager->GetInventoryContainer((InventoryType)((int)InventoryType.Inventory1 + page));
            if (bag == null || !bag->IsLoaded || bag->Items == null || bag->Size != 35) return false;
            for (var index = 0; index < bag->Size; index++)
            {
                var item = bag->Items[index];
                if (item.IsSymbolic || item.ItemId == 0 || item.Quantity <= 0 || !sheet.TryGetRow(item.ItemId, out var data)) continue;
                var fingerprint = $"{item.Condition}:{item.SpiritbondOrCollectability}:{item.CrafterContentId}:{item.GlamourId}"
                    + $":{string.Join(',', item.Materia.ToArray())}:{string.Join(',', item.MateriaGrades.ToArray())}:{string.Join(',', item.Stains.ToArray())}";
                result.Add(new(page, index, item.ItemId, item.Quantity, (byte)item.Flags, fingerprint, data.PriceLow, data.Name.ExtractText()));
            }
        }
        return true;
    }

    private static bool TryContext(out SaleContext value)
    {
        value = default;
        if (!Plugin.Player.IsLoaded || Plugin.Conditions[ConditionFlag.InCombat] || Plugin.Conditions[ConditionFlag.BetweenAreas]) return false;
        var shop = Addon("Shop");
        var retainerBag = Addon("InventoryRetainerLarge");
        if (retainerBag == null) retainerBag = Addon("InventoryRetainer");
        var framework = GameFramework.Instance();
        var order = framework == null || framework->UIModule == null ? null : framework->UIModule->GetItemOrderModule();
        var activeRetainerId = order == null ? 0UL : order->ActiveRetainerId;
        if (shop != null && (retainerBag != null || activeRetainerId != 0)) return false;
        if (shop != null)
        {
            var agent = AgentShop.Instance();
            var proxy = ShopEventHandler.AgentProxy.Instance();
            if (agent == null || !agent->IsAgentActive() || proxy == null || proxy->Handler == null
                || proxy->AddonId != shop->Id || proxy->Handler->CurrentMode != 1) return false;
            value = new(1, (nint)proxy->Handler, (nint)shop, shop->Id, Plugin.Player.ContentId, Plugin.ClientState.TerritoryType, 0);
            return true;
        }
        var retainerAgent = AgentRetainer.Instance();
        var manager = RetainerManager.Instance();
        var retainer = manager == null ? null : manager->GetActiveRetainer();
        if (SaleRules.RetainerAvailable(retainerAgent != null && retainerAgent->IsAgentActive(),
            activeRetainerId, retainer == null ? 0UL : retainer->RetainerId))
        {
            // The live retainer interaction already provides Have Retainer Sell Items.
            // Keep ownership across its main menu and inventory panels; LastSelectedRetainerId
            // alone survives dismissal and must never make the recycling button available.
            value = new(2, (nint)retainerAgent, 0, 0, Plugin.Player.ContentId,
                Plugin.ClientState.TerritoryType, activeRetainerId);
            return true;
        }
        return false;
    }

    private static ShopEventHandler* Handler(SaleContext value) => value.Kind == 1 ? (ShopEventHandler*)value.Agent
        : value.Kind == 2 ? ((AgentRetainer*)value.Agent)->ShopEventHandler : null;

    private static bool Busy(SaleContext value)
    {
        var handler = Handler(value);
        return handler != null && (handler->StartingBuy || handler->StartingSell || handler->UpdatingBuybackItems
            || handler->WaitingForSellConfirm || handler->WaitingForTransactionToFinish);
    }

    private static ulong Gil(SaleContext value)
    {
        if (value.Kind == 1) return InventoryManager.Instance()->GetGil();
        return InventoryManager.Instance()->GetGil() + (ulong)RetainerGil(value);
    }

    private static uint RetainerGil(SaleContext value)
    {
        var manager = RetainerManager.Instance();
        var retainer = manager == null ? null : manager->GetActiveRetainer();
        if (retainer == null || retainer->RetainerId != value.Retainer) throw new InvalidOperationException("Active retainer changed.");
        return retainer->Gil;
    }

    private static bool HasGilRoom(SaleSlot item, SaleContext value) => SaleRules.CanFit(item, InventoryManager.Instance()->GetGil())
        && (value.Kind != 2 || SaleRules.CanFit(item, RetainerGil(value)));

    private static bool HasDialog() => Addon("InputNumeric") != null || Addon("SelectYesno") != null || Addon("RetainerSell") != null;

    private static AtkUnitBase* BagAddon()
    {
        foreach (var name in new[] { "InventoryExpansion", "InventoryLarge", "Inventory" })
        {
            var addon = Addon(name);
            if (addon != null) return addon;
        }
        return null;
    }

    private static AtkUnitBase* Addon(string name)
    {
        var handle = Plugin.GameGui.GetAddonByName(name);
        return handle.IsNull || !handle.IsReady || !handle.IsVisible ? null : (AtkUnitBase*)handle.Address;
    }

    private static void Callback(AtkUnitBase* addon, ReadOnlySpan<int> values)
    {
        var args = stackalloc AtkValue[values.Length];
        for (var i = 0; i < values.Length; i++) { args[i] = default; args[i].Type = AtkValueType.Int; args[i].Int = values[i]; }
        addon->FireCallback((uint)values.Length, args, true);
    }

    private void FinalizeContext(AddonEvent type, AddonArgs args)
    {
        if (IsRunning && args.Addon.Address == context.Addon) Stop("出售窗口已关闭，已停止。");
    }

    private void Notify(string text)
    {
        Status = text;
        Plugin.Chat.Print($"[回收之王] {text}");
    }

    private void Stop(string reason)
    {
        if (!IsRunning) return;
        IsRunning = false;
        queue.Clear();
        Notify($"{reason} 已确认出售 {soldStacks} 组，共 {soldItems} 件。");
    }

    public void Dispose()
    {
        Stop("插件已关闭，已停止；已提交的出售仍可能完成。");
        Plugin.Framework.Update -= Tick;
        Plugin.Addons.UnregisterListener(AddonEvent.PreFinalize, ContextAddons, FinalizeContext);
        Plugin.Addons.UnregisterListener(AddonEvent.PostSetup, SaleAddons, SaleAddonReady);
        Plugin.Addons.UnregisterListener(AddonEvent.PostUpdate, SaleAddons, SaleAddonReady);
    }
}
