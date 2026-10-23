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
    private readonly Action open;
    private long retryAfter;

    internal InventoryButton(Action open)
    {
        this.open = open;
        Plugin.Addons.RegisterListener(AddonEvent.PreFinalize, AddonNames, OnFinalize);
        Plugin.Framework.Update += Update;
    }

    private void Update(IFramework framework)
    {
        if (Environment.TickCount64 < retryAfter) return;
        try
        {
            foreach (var name in AddonNames)
            {
                var handle = Plugin.GameGui.GetAddonByName(name);
                if (handle.IsNull || !handle.IsReady) continue;
                var addon = (AtkUnitBase*)handle.Address;
                if (addon->RootNode == null) continue;
                if (!buttons.TryGetValue(name, out var button))
                {
                    if (!handle.IsVisible) continue;
                    button = new TextButtonNode
                    {
                        String = "回收废品", Size = new Vector2(112, 28), OnClick = open,
                        TextTooltip = "打开回收之王物品清单",
                    };
                    try { button.AttachNode(addon); buttons.Add(name, button); }
                    catch { button.Dispose(); throw; }
                }
                // Coordinates are local to the native root, so movement and UI scaling follow the bag.
                button.Position = new Vector2(12, Math.Max(0, addon->RootNode->Height - 50));
                button.IsVisible = handle.IsVisible;
            }
        }
        catch (Exception exception)
        {
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
