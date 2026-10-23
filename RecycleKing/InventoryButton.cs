using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;

namespace RecycleKing;

/// <summary>Owns one native node per inventory addon, detached before the addon is freed.</summary>
internal sealed unsafe class InventoryButton : IDisposable
{
    private static readonly string[] AddonNames = ["Inventory", "InventoryLarge", "InventoryExpansion"];
    private readonly Dictionary<string, TextButtonNode> buttons = [];
    private readonly Plugin plugin;
    private long retryAfter;

    internal InventoryButton(Plugin plugin)
    {
        this.plugin = plugin;
        Plugin.Addons.RegisterListener(AddonEvent.PreFinalize, AddonNames, OnFinalize);
        Plugin.Framework.Update += Update;
    }

    private void Update(IFramework framework)
    {
        if (Environment.TickCount64 < retryAfter) return;
        try
        {
            var show = plugin.Sales.ShowButton;
            var canStart = plugin.Sales.CanStart;
            foreach (var name in AddonNames)
            {
                var handle = Plugin.GameGui.GetAddonByName(name);
                if (handle.IsNull || !handle.IsReady) continue;
                var addon = (AtkUnitBase*)handle.Address;
                if (addon->RootNode == null) continue;
                if (!buttons.TryGetValue(name, out var button))
                {
                    if (!handle.IsVisible || !show) continue;
                    button = new TextButtonNode
                    {
                        String = "废品回收", Size = new Vector2(112, 28), OnClick = plugin.Sales.Toggle,
                    };
                    try { button.AttachNode(addon); buttons.Add(name, button); }
                    catch { button.Dispose(); throw; }
                }
                // Coordinates are local to the native root, so movement and UI scaling follow the bag.
                button.Position = new Vector2(12, Math.Max(0, addon->RootNode->Height - 50));
                button.IsVisible = handle.IsVisible && show;
                button.String = plugin.Sales.IsRunning ? "停止回收" : "废品回收";
                button.IsEnabled = plugin.Sales.IsRunning || (canStart && plugin.Config.ItemIds.Count > 0);
                button.TextTooltip = plugin.Sales.IsRunning ? plugin.Sales.Status + "\n点击停止；已提交的出售仍可能完成。"
                    : !canStart ? "请先完成当前出售或价格调整，再开始废品回收。"
                    : "点击即批量出售普通背包中的清单物品，包含 HQ 和整组数量。\n管理清单请打开插件设置或输入 /rking。";
            }
        }
        catch (Exception exception)
        {
            foreach (var button in buttons.Values) button.IsVisible = false;
            retryAfter = Environment.TickCount64 + 5000;
            Plugin.Log.Error(exception, "回收之王无法添加背包按钮，将稍后重试。");
        }
    }

    private void OnFinalize(AddonEvent type, AddonArgs args)
    {
        if (buttons.Remove(args.AddonName, out var button)) button.Dispose();
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= Update;
        Plugin.Addons.UnregisterListener(AddonEvent.PreFinalize, AddonNames, OnFinalize);
        foreach (var button in buttons.Values) button.Dispose();
        buttons.Clear();
    }
}
