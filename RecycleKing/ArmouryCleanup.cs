using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using static RecycleKing.NativeInventory;

namespace RecycleKing;

/// <summary>Moves a finite armoury snapshot to free normal-bag slots, excluding saved gearsets.</summary>
internal sealed unsafe class ArmouryCleanup : IDisposable
{
    private const string AddonName = "ArmouryBoard";
    private readonly Plugin plugin;
    private readonly OperationTrace trace = new("垃圾出库");
    private readonly Queue<DesynthItem> queue = [];
    private TextButtonNode? button;
    private nint addonAddress;
    private ushort addonId;
    private HashSet<uint> registered = [];
    private ulong character;
    private uint territory;
    private DesynthItem current;
    private int targetInventory;
    private int targetSlot;
    private long deadline;
    private long stableAfter;
    private int moved;
    private bool pending;
    private bool updating;
    internal bool IsRunning { get; private set; }

    internal ArmouryCleanup(Plugin plugin)
    {
        this.plugin = plugin;
        Plugin.Framework.Update += Update;
        Plugin.Addons.RegisterListener(AddonEvent.PreFinalize, AddonName, FinalizeAddon);
    }

    private static AtkUnitBase* GetAddon()
    {
        var handle = Plugin.GameGui.GetAddonByName(AddonName);
        return handle.IsNull || !handle.IsReady ? null : (AtkUnitBase*)handle.Address;
    }

    private bool Available() => Plugin.Player.IsLoaded && Plugin.Player.ContentId != 0
        && !Plugin.Conditions[ConditionFlag.InCombat] && !Plugin.Conditions[ConditionFlag.BetweenAreas]
        && !Plugin.Conditions[ConditionFlag.BetweenAreas51] && !Plugin.Conditions[ConditionFlag.OccupiedInEvent]
        && !plugin.Sales.IsRunning && !plugin.Desynthesis.IsRunning;

    private void Update(IFramework framework)
    {
        if (updating) return;
        updating = true;
        try
        {
            var addon = GetAddon();
            if (button != null && (addon == null || (nint)addon != addonAddress || addon->Id != addonId))
            { Stop("兵装库窗口已变化，已停止。"); return; }
            if (addon != null && addon->RootNode != null)
            {
                if (button == null && addon->IsVisible)
                {
                    addonAddress = (nint)addon;
                    addonId = addon->Id;
                    button = new TextButtonNode { String = "垃圾出库", Size = new Vector2(112, 28), OnClick = Toggle };
                    try { button.AttachNode(addon); }
                    catch { button.Dispose(); button = null; throw; }
                }
                if (button != null)
                {
                    button.Position = new Vector2(12, Math.Max(0, addon->RootNode->Height - 40));
                    button.IsVisible = addon->IsVisible;
                    button.IsEnabled = IsRunning || Available();
                    button.String = IsRunning ? "停止出库" : "垃圾出库";
                }
            }
            if (IsRunning) Advance(addon);
        }
        catch (Exception error)
        {
            Stop("装备状态无法核对，已停止出库。");
            Plugin.Log.Error(error, "回收之王垃圾出库失败。");
        }
        finally { updating = false; }
    }

    private void Toggle()
    {
        if (IsRunning) { Stop("已停止；已提交的移动仍可能完成。"); return; }
        try
        {
            var addon = GetAddon();
            if (!Available() || addon == null || !addon->IsVisible || (nint)addon != addonAddress || addon->Id != addonId)
            { Notify("请先结束当前操作，再开始垃圾出库。"); return; }
            if (!TryGearsets(out registered)) { Notify("装备套装数据尚未就绪，未开始出库。"); return; }
            if (!TryArmoury(out var items)) { Notify("兵装库数据尚未就绪，未开始出库。"); return; }
            queue.Clear();
            foreach (var item in items)
                if (ArmouryMoveRules.Eligible(registered, item)) queue.Enqueue(item);
            if (queue.Count == 0) { Notify("兵装库中没有未登记套装的散件。"); return; }
            if (!FirstEmptyBagSlot(out _, out _)) { Notify("背包已满，未开始出库。"); return; }
            character = Plugin.Player.ContentId;
            territory = Plugin.ClientState.TerritoryType;
            moved = 0;
            pending = false;
            stableAfter = Environment.TickCount64 + 200;
            IsRunning = true;
            trace.Start($"items={queue.Count} protectedIdentities={registered.Count}");
        }
        catch (Exception error)
        {
            Plugin.Log.Error(error, "回收之王无法开始垃圾出库。");
            Notify("兵装库状态无法核对，未开始出库。");
        }
    }

    private void Advance(AtkUnitBase* addon)
    {
        if (!Available() || Plugin.Player.ContentId != character || Plugin.ClientState.TerritoryType != territory
            || addon == null || !addon->IsVisible || (nint)addon != addonAddress || addon->Id != addonId)
        { Stop("窗口、角色或操作状态已变化，已停止。"); return; }
        if (!TryGearsets(out var active) || !registered.SetEquals(active))
        { Stop("装备套装登记已变化，已停止。"); return; }
        if (Environment.TickCount64 < stableAfter) return;
        if (pending)
        {
            if (!TryReadSlot(current.Inventory, current.Slot, out var source)
                || !TryReadSlot(targetInventory, targetSlot, out var target))
            { Stop("物品栏数据不可用，已停止。"); return; }
            if (source == null && target is { } arrived && ArmouryMoveRules.Arrived(current, arrived))
            {
                moved++;
                trace.Info("确认移入背包", $"item={current.Id} targetInventory={targetInventory} targetSlot={targetSlot} moved={moved}");
                pending = false;
                queue.Dequeue();
                return;
            }
            if (source != null && source != current || target != null && !ArmouryMoveRules.Arrived(current, target.Value))
            { Stop("待移动物品或目标格子已变化，已停止。"); return; }
            if (Environment.TickCount64 >= deadline) Stop("未确认移动结果，已停止，不会重试该物品。");
            return;
        }
        if (queue.Count == 0) { Stop("垃圾出库完成。"); return; }
        // Revalidate every remaining captured source before submitting another move.
        foreach (var item in queue)
            if (ReadSlot(item.Inventory, item.Slot) != item || ArmouryMoveRules.Protected(active, item))
            { Stop("待出库物品或套装引用已变化，已停止。"); return; }
        if (!FirstEmptyBagSlot(out targetInventory, out targetSlot)) { Stop("背包已满，已停止。"); return; }
        var manager = InventoryManager.Instance();
        if (manager == null) { Stop("物品栏数据不可用，已停止。"); return; }
        current = queue.Peek();
        pending = true;
        deadline = Environment.TickCount64 + 8000;
        trace.Info("提交移动", $"item={current.Id} inventory={current.Inventory} slot={current.Slot} targetInventory={targetInventory} targetSlot={targetSlot}");
        manager->MoveItemSlot((InventoryType)current.Inventory, (ushort)current.Slot,
            (InventoryType)targetInventory, (ushort)targetSlot, true);
    }

    private static bool TryGearsets(out HashSet<uint> ids)
    {
        ids = [];
        var module = RaptureGearsetModule.Instance();
        if (module == null) return false;
        foreach (ref var set in module->Entries)
            if ((set.Flags & RaptureGearsetModule.GearsetFlag.Exists) != 0)
                foreach (ref var item in set.Items)
                    if (item.ItemId != 0) ids.Add(item.ItemId);
        return true;
    }

    private static bool TryArmoury(out List<DesynthItem> items)
    {
        items = [];
        var manager = InventoryManager.Instance();
        if (manager == null) return false;
        foreach (var type in ArmouryTypes)
        {
            var container = manager->GetInventoryContainer(type);
            if (container == null || !container->IsLoaded || container->Type != type
                || container->Size is < 1 or > 100 || container->Items == null) return false;
            for (var i = 0; i < container->Size; i++)
            {
                if (!TryReadSlot((int)type, i, out var item)) return false;
                if (item is { } value) items.Add(value);
            }
        }
        return true;
    }

    private static bool FirstEmptyBagSlot(out int inventory, out int slot)
    {
        inventory = slot = -1;
        var manager = InventoryManager.Instance();
        if (manager == null) return false;
        // Require all four bags before considering any destination available.
        for (var page = 0; page < 4; page++)
        {
            var bag = manager->GetInventoryContainer((InventoryType)page);
            if (bag == null || !bag->IsLoaded || bag->Type != (InventoryType)page || bag->Size != 35 || bag->Items == null) return false;
            for (var i = 0; i < bag->Size; i++)
                if (inventory < 0 && bag->Items[i].ItemId == 0) { inventory = page; slot = i; }
        }
        return inventory >= 0;
    }

    private void Notify(string text)
    {
        trace.Info("提示", text);
        Plugin.Chat.Print($"[回收之王] {text}");
    }

    private void Stop(string reason)
    {
        if (!IsRunning) return;
        trace.Info("停止", $"reason={reason} pending={pending} moved={moved} remaining={queue.Count}");
        IsRunning = false;
        queue.Clear();
        Notify($"{reason} 已确认移入背包 {moved} 件。");
    }

    private void FinalizeAddon(AddonEvent type, AddonArgs args)
    {
        if (args.Addon.Address != addonAddress) return;
        Stop("兵装库已关闭，已停止；已提交的移动仍可能完成。");
        button?.Dispose();
        button = null;
        addonAddress = 0;
        addonId = 0;
    }

    public void Dispose()
    {
        Stop("插件已关闭，已停止；已提交的移动仍可能完成。");
        Plugin.Framework.Update -= Update;
        Plugin.Addons.UnregisterListener(AddonEvent.PreFinalize, AddonName, FinalizeAddon);
        button?.Dispose();
        button = null;
    }
}
