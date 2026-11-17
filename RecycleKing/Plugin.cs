using Dalamud.Game.Command;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KamiToolKit;
using Item = Lumina.Excel.Sheets.Item;

namespace RecycleKing;

public sealed class Plugin : IAsyncDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle Addons { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IPlayerState Player { get; private set; } = null!;
    [PluginService] internal static ICondition Conditions { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IToastGui Toasts { get; private set; } = null!;

    internal Configuration Config { get; }
    internal int Revision { get; private set; }
    internal BatchSale Sales { get; private set; } = null!;
    internal BatchDesynthesis Desynthesis { get; private set; } = null!;
    internal ArmouryCleanup Armoury { get; private set; } = null!;
    private NativeListWindow? window;
    private InventoryButton? inventoryButton;
    private ExpertDeliveryLevels? deliveryLevels;
    private AutoRetainerOverlayHider? overlayHider;
    private RecycleBagOverlay? bagOverlay;
    private uint[] itemSnapshot = [];
    internal const string HighlightIpcName = "RecycleKing.GetHighlightedItemIds";
    private volatile bool disposing;
    private RecycleKingIpc? ipc;
    internal bool IsDisposing => disposing;
    private static readonly string[] CommandNames = ["/recycleking", "/rking"];

    public Plugin()
    {
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new();
        Config.ItemIds = RecycleList.Normalize(Config.ItemIds ?? []);
        itemSnapshot = Config.ItemIds.ToArray();
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        await KamiToolKitLibrary.InitializeAsync(PluginInterface);
        cancellationToken.ThrowIfCancellationRequested();
        await Framework.Run(() =>
        {
            window = new NativeListWindow(this)
            {
                InternalName = "RecycleKingList", Title = "回收之王", Size = new(510, 530),
            };
            Sales = new BatchSale(this);
            Desynthesis = new BatchDesynthesis(this);
            Armoury = new ArmouryCleanup(this);
            inventoryButton = new InventoryButton(this);
            deliveryLevels = new ExpertDeliveryLevels(this);
            overlayHider = new AutoRetainerOverlayHider(this);
            bagOverlay = new RecycleBagOverlay(this);
            PluginInterface.UiBuilder.Draw += DrawOverlays;
            PluginInterface.GetIpcProvider<uint[]>(HighlightIpcName).RegisterFunc(() => HighlightedItemIds().ToArray());
            ipc = new RecycleKingIpc(this);
            ipc.Register();
            foreach (var command in CommandNames)
                Commands.AddHandler(command, new CommandInfo((_, _) => OpenList()) { HelpMessage = "打开回收之王物品清单。" });
            ContextMenu.OnMenuOpened += OnMenuOpened;
            PluginInterface.UiBuilder.OpenMainUi += OpenList;
            PluginInterface.UiBuilder.OpenConfigUi += OpenList;
            Log.Information("RecycleKing {Version} 已加载；操作日志可在 /xllog 按 RecycleKing 筛选。", PluginInterface.Manifest.AssemblyVersion);
        });
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        if (disposing || args.Target is not MenuTargetInventory { TargetItem: { } target }) return;
        var id = RecycleList.BaseId(target.ItemId);
        if (id is 0 or >= 500_000 || !Data.GetExcelSheet<Item>().TryGetRow(id, out _)) return;
        args.AddMenuItem(new MenuItem
        {
            Priority = int.MaxValue,
            Name = "加入回收清单", PrefixChar = 'K', PrefixColor = RecycleAppearance.PrefixColor,
            IsEnabled = !Config.ItemIds.Contains(id),
            OnClicked = _ => Queue(() => AddItem(id)),
        });
    }

    internal bool AddItem(uint rawId)
    {
        var id = RecycleList.BaseId(rawId);
        if (id is 0 or >= 500_000 || !Data.GetExcelSheet<Item>().TryGetRow(id, out _)
            || !RecycleList.Add(Config.ItemIds, id)) return false;
        Save();
        return true;
    }

    internal void Remove(uint id) => RemoveItem(id);

    internal bool RemoveItem(uint id)
    {
        if (!Config.ItemIds.Remove(RecycleList.BaseId(id))) return false;
        Save();
        return true;
    }

    internal bool Clear()
    {
        if (Config.ItemIds.Count == 0) return false;
        Config.ItemIds.Clear();
        Save();
        return true;
    }

    internal bool IsListOpen => window?.IsOpen == true;

    internal bool SetListOpen(bool open)
    {
        if (window is null) return false;
        if (open) window.RequestOpen();
        else window.Close();
        return true;
    }

    internal bool SetHighlightListedItems(bool enabled)
    {
        if (Config.HighlightListedItems == enabled) return true;
        Config.HighlightListedItems = enabled;
        Save();
        return true;
    }

    internal bool SetHideAutoRetainerGcOverlay(bool enabled)
    {
        if (Config.HideAutoRetainerGcOverlay == enabled) return true;
        Config.HideAutoRetainerGcOverlay = enabled;
        Save();
        return true;
    }

    internal bool SetShowExpertDeliveryLevels(bool enabled)
    {
        if (Config.ShowExpertDeliveryLevels == enabled) return true;
        Config.ShowExpertDeliveryLevels = enabled;
        Save();
        return true;
    }

    internal void Save()
    {
        itemSnapshot = Config.ItemIds.ToArray();
        Revision++;
        PluginInterface.SavePluginConfig(Config);
    }

    internal void OpenList() => Queue(() => SetListOpen(true));
    internal uint[] HighlightedItemIds() => Config.HighlightListedItems ? itemSnapshot : [];

    private void DrawOverlays()
    {
        if (disposing) return;
        overlayHider?.Draw();
        bagOverlay?.Draw();
    }

    private void Queue(Action action) => _ = Framework.Run(() =>
    {
        if (disposing) return;
        try { action(); }
        catch (Exception exception) { Log.Error(exception, "回收之王操作失败。"); }
    });

    public async ValueTask DisposeAsync()
    {
        disposing = true;
        ipc?.Dispose();
        PluginInterface.GetIpcProvider<uint[]>(HighlightIpcName).UnregisterFunc();
        PluginInterface.UiBuilder.Draw -= DrawOverlays;
        overlayHider?.Dispose();
        await Framework.Run(() =>
        {
            ContextMenu.OnMenuOpened -= OnMenuOpened;
            PluginInterface.UiBuilder.OpenMainUi -= OpenList;
            PluginInterface.UiBuilder.OpenConfigUi -= OpenList;
            foreach (var command in CommandNames) Commands.RemoveHandler(command);
            window?.StopPendingOpen();
            Sales?.Dispose();
            Desynthesis?.Dispose();
            Armoury?.Dispose();
            inventoryButton?.Dispose();
            deliveryLevels?.Dispose();
        });
        if (window is not null) await window.DisposeAsync();
        await KamiToolKitLibrary.DisposeAsync();
    }
}
