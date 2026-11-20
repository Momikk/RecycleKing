using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Interfaces;
using KamiToolKit.Nodes;
using Lumina.Data.Parsing.Uld;
using Item = Lumina.Excel.Sheets.Item;

namespace RecycleKing;

internal sealed record RecycleRow(uint Id, uint Icon, string Name, SaleStatisticsCells Statistics, Action<uint> Remove, Action<uint> Update);

internal static class RecycleListColumns
{
    // KamiToolKit reserves 16 pixels for the list's scroll bar.
    internal static float DateX(float rowWidth) => rowWidth - 442;
    internal static float PriceX(float rowWidth) => rowWidth - 352;
    internal static float DaysX(float rowWidth) => rowWidth - 256;
    internal static float AgeX(float rowWidth) => rowWidth - 190;
    internal static float UpdateX(float rowWidth) => rowWidth - 124;
    internal static float DeleteX(float rowWidth) => rowWidth - 62;
}

internal sealed unsafe class NativeListWindow : NativeAddon
{
    private readonly Plugin plugin;
    private ListNode<RecycleRow, RecycleRowNode>? list;
    private TextNode? count;
    private ResNode? heading;
    private TextButtonNode? clear;
    private TextButtonNode? cancel;
    private TextButtonNode? refreshAll;
    private TextNode? progress;
    private CheckboxNode? hideOverlay;
    private CheckboxNode? highlight;
    private int revision = -1;
    private bool confirmClear;
    private bool pendingOpen;
    private long refreshAgeAfter;
    private RecycleSortColumn sortColumn = RecycleSortColumn.Name;
    private bool sortDescending;
    private readonly Dictionary<RecycleSortColumn, TextNode> sortHeaders = new();
    private string? countText;
    private string? clearText;
    private string? refreshText;
    private string? progressText;

    internal NativeListWindow(Plugin plugin)
    {
        this.plugin = plugin;
        Plugin.Framework.Update += Tick;
    }

    internal void RequestOpen()
    {
        if (IsOpen) return;
        pendingOpen = true;
        Tick(null!);
    }

    private void Tick(Dalamud.Plugin.Services.IFramework framework)
    {
        if (!pendingOpen || IsAllocated) return;
        pendingOpen = false;
        Open();
    }

    public override void Close()
    {
        pendingOpen = false;
        plugin.MarketRefresh.Stop();
        base.Close();
    }

    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        if (WindowNode is WindowNode titleBar) titleBar.CloseButtonNode.OnClick = Close;
        var origin = ContentStartPosition;
        var width = ContentSize.X;
        hideOverlay = new CheckboxNode
        {
            Position = origin, Size = new(width, 24), String = "隐藏 AutoRetainer 军票悬浮框",
            IsChecked = plugin.Config.HideAutoRetainerGcOverlay,
            TextTooltip = "仅隐藏悬浮框，不会停止 AutoRetainer 已启用的自动交纳。关闭本选项可恢复；插件更新后可能需要适配。",
            OnClick = value => plugin.SetHideAutoRetainerGcOverlay(value),
        };
        AddNode(hideOverlay);
        highlight = new CheckboxNode
        {
            Position = origin + new Vector2(0, 27), Size = new(width, 24), String = "背包清单物品显示粉色遮罩与 X",
            IsChecked = plugin.Config.HighlightListedItems,
            OnClick = value => plugin.SetHighlightListedItems(value),
        };
        AddNode(highlight);
        heading = new ResNode { Position = origin + new Vector2(0, 56), Size = new(width, 28) };
        AddNode(heading);
        var rowWidth = width - 16;
        foreach (var (label, x, cellWidth, column) in new (string, float, float, RecycleSortColumn?)[]
        {
            ("图标", 0f, 40f, null),
            ("物品名称", 44f, RecycleListColumns.DateX(rowWidth) - 50, RecycleSortColumn.Name),
            ("最早售出", RecycleListColumns.DateX(rowWidth), 86f, RecycleSortColumn.OldestSale),
            ("平均价格", RecycleListColumns.PriceX(rowWidth), 90f, RecycleSortColumn.AverageUnitPrice),
            ("距统计", RecycleListColumns.DaysX(rowWidth), 60f, RecycleSortColumn.DaysAtObservation),
            ("更新距今", RecycleListColumns.AgeX(rowWidth), 60f, RecycleSortColumn.UpdatedDaysAgo),
            ("操作", RecycleListColumns.UpdateX(rowWidth), 118f, null),
        })
        {
            var text = new TextNode
            {
                Position = new(x, 0), Size = new(cellWidth, 28),
                FontSize = column == RecycleSortColumn.UpdatedDaysAgo ? (byte)12 : (byte)13,
                AlignmentType = AlignmentType.Left, String = label,
            };
            text.AttachNode(heading);
            if (column is { } sortable)
            {
                text.ShowClickableCursor = true;
                text.AddEvent(AtkEventType.MouseClick, () => ChangeSort(sortable));
                sortHeaders.Add(sortable, text);
            }
        }
        list = new ListNode<RecycleRow, RecycleRowNode>
        {
            Position = origin + new Vector2(0, 86), Size = new(width, ContentSize.Y - 158),
            OptionsList = [], AutoResetScroll = false, ItemSpacing = 2,
        };
        list.NoResultsTextNode.TextId = 0;
        list.NoResultsTextNode.String = "清单为空，请右键物品加入回收清单。";
        AddNode(list);
        count = new TextNode
        {
            Position = origin + new Vector2(0, ContentSize.Y - 66), Size = new(width, 24),
            FontSize = 12, AlignmentType = AlignmentType.Left,
        };
        AddNode(count);
        clear = new TextButtonNode
        {
            Position = origin + new Vector2(0, ContentSize.Y - 32), Size = new(136, 28),
            String = "清空清单", OnClick = () =>
            {
                if (plugin.MarketRefresh.IsRunning) return;
                if (confirmClear) { plugin.Clear(); confirmClear = false; }
                else confirmClear = true;
                UpdateControls();
            },
        };
        AddNode(clear);
        cancel = new TextButtonNode
        {
            Position = origin + new Vector2(146, ContentSize.Y - 32), Size = new(80, 28),
            String = "取消", IsVisible = false, OnClick = () => { confirmClear = false; UpdateControls(); },
        };
        AddNode(cancel);
        progress = new TextNode
        {
            Position = origin + new Vector2(240, ContentSize.Y - 32), Size = new(Math.Max(1, width - 360), 28),
            FontSize = 12, AlignmentType = AlignmentType.Left, TextFlags = TextFlags.Ellipsis,
        };
        AddNode(progress);
        refreshAll = new TextButtonNode
        {
            Position = origin + new Vector2(width - 112, ContentSize.Y - 32), Size = new(108, 28),
            String = "更新全部", OnClick = ToggleRefresh,
        };
        AddNode(refreshAll);
        revision = -1;
        Refresh();
    }

    protected override void OnUpdate(AtkUnitBase* addon)
    {
        if (revision != plugin.Revision) { confirmClear = false; Refresh(); }
        else if (!confirmClear && plugin.Config.SaleStatistics.Count > 0 && Environment.TickCount64 >= refreshAgeAfter) Refresh();
        UpdateControls();
    }

    private void Refresh()
    {
        if (list is null) return;
        if (hideOverlay is not null) hideOverlay.IsChecked = plugin.Config.HideAutoRetainerGcOverlay;
        if (highlight is not null) highlight.IsChecked = plugin.Config.HighlightListedItems;
        var sheet = Plugin.Data.GetExcelSheet<Item>();
        var now = DateTimeOffset.UtcNow;
        list.OptionsList = RecycleSort.Sort(plugin.Config.ItemIds.Select(id => sheet.TryGetRow(id, out var item)
            ? new RecycleRow(id, item.Icon, item.Name.ExtractText(), Statistics(id), RemoveItem, RefreshItem)
            : new RecycleRow(id, 0, $"未知物品 #{id}", Statistics(id), RemoveItem, RefreshItem)),
            row => row.Name, row => row.Id, row => plugin.Config.SaleStatistics.GetValueOrDefault(row.Id),
            sortColumn, sortDescending, now);
        SaleStatisticsCells Statistics(uint id) => SaleStatisticsDisplay.Format(plugin.Config.SaleStatistics.GetValueOrDefault(id), now, TimeZoneInfo.Local);
        revision = plugin.Revision;
        refreshAgeAfter = Environment.TickCount64 + 60_000;
        UpdateSortHeaders();
        UpdateControls();
    }

    private void ChangeSort(RecycleSortColumn column)
    {
        sortDescending = sortColumn == column && !sortDescending;
        sortColumn = column;
        confirmClear = false;
        Refresh();
        list?.ResetScroll();
    }

    private static string SortLabel(RecycleSortColumn column) => column switch
    {
        RecycleSortColumn.Name => "物品名称",
        RecycleSortColumn.OldestSale => "最早售出",
        RecycleSortColumn.AverageUnitPrice => "平均价格",
        RecycleSortColumn.DaysAtObservation => "距统计",
        RecycleSortColumn.UpdatedDaysAgo => "更新距今",
        _ => "物品名称",
    };

    private void UpdateSortHeaders()
    {
        foreach (var (column, text) in sortHeaders)
            text.String = SortLabel(column) + (column == sortColumn ? sortDescending ? "↓" : "↑" : "");
    }

    private void UpdateControls()
    {
        if (clear is null || cancel is null || count is null || refreshAll is null || progress is null) return;
        var running = plugin.MarketRefresh.IsRunning;
        RespectCloseAll = !running;
        if (running) confirmClear = false;
        SetText(clear.LabelNode, confirmClear ? "确认清空清单" : "清空清单", ref clearText);
        var clearEnabled = !running && plugin.Config.ItemIds.Count > 0;
        if (clear.IsEnabled != clearEnabled) clear.IsEnabled = clearEnabled;
        if (cancel.IsVisible != confirmClear) cancel.IsVisible = confirmClear;
        var order = (sortColumn == RecycleSortColumn.Name ? "拼音" : SortLabel(sortColumn)) + (sortDescending ? "降序" : "升序");
        SetText(count, confirmClear ? "再次点击确认清空，仅移除清单记录。" : $"共 {plugin.Config.ItemIds.Count} 种物品 · {order} · HQ/NQ 合并", ref countText);
        SetText(refreshAll.LabelNode, running ? "停止更新" : "更新全部", ref refreshText);
        var refreshEnabled = running || plugin.Config.ItemIds.Count > 0;
        if (refreshAll.IsEnabled != refreshEnabled) refreshAll.IsEnabled = refreshEnabled;
        SetText(progress, plugin.MarketRefresh.Status, ref progressText);
        if (list is not null)
            foreach (var row in list.OptionNodes) row.SetUpdating(running);
    }

    private static void SetText(TextNode node, string text, ref string? previous)
    {
        if (previous == text) return;
        node.String = text;
        previous = text;
    }

    private void RefreshItem(uint itemId)
    {
        if (plugin.MarketRefresh.IsRunning) return;
        confirmClear = false;
        StartMarketRefresh(itemId);
    }

    private void ToggleRefresh()
    {
        confirmClear = false;
        if (!plugin.MarketRefresh.IsRunning)
        {
            StartMarketRefresh(null);
            return;
        }
        plugin.MarketRefresh.Stop();
        UpdateControls();
    }

    private void StartMarketRefresh(uint? itemId)
    {
        // Opening ItemSearch can issue Close All before Start returns.
        RespectCloseAll = false;
        plugin.MarketRefresh.Start(itemId);
        UpdateControls();
    }

    private void RemoveItem(uint itemId)
    {
        if (!plugin.MarketRefresh.IsRunning) plugin.Remove(itemId);
    }

    protected override void OnFinalize(AtkUnitBase* addon)
    {
        // The game can release this window while opening the market board.
        // The query owns the market session independently of the list window.
        list?.Dispose(); list = null;
        heading?.Dispose(); heading = null;
        sortHeaders.Clear();
        count?.Dispose(); count = null;
        clear?.Dispose(); clear = null;
        cancel?.Dispose(); cancel = null;
        refreshAll?.Dispose(); refreshAll = null;
        progress?.Dispose(); progress = null;
        hideOverlay?.Dispose(); hideOverlay = null;
        highlight?.Dispose(); highlight = null;
        confirmClear = false;
        countText = clearText = refreshText = progressText = null;
    }

    internal void StopPendingOpen()
    {
        pendingOpen = false;
        Plugin.Framework.Update -= Tick;
    }
}

internal sealed class RecycleRowNode : ListItemNode<RecycleRow>, IListItemNode
{
    public static float ItemHeight => 36;
    private readonly IconImageNode icon;
    private readonly TextNode name;
    private readonly TextNode oldestSale;
    private readonly TextNode averagePrice;
    private readonly TextNode daysAtObservation;
    private readonly TextNode updatedDaysAgo;
    private readonly TextButtonNode update;
    private readonly TextButtonNode remove;
    private RecycleRow? current;
    private bool? updating;

    public RecycleRowNode()
    {
        EnableSelection = false;
        icon = new IconImageNode { Position = new(2, 2), Size = new(32), FitTexture = true };
        icon.AttachNode(this);
        name = new TextNode
        {
            Position = new(44, 0), FontSize = 13, AlignmentType = AlignmentType.Left, TextFlags = TextFlags.Ellipsis,
        };
        name.AttachNode(this);
        oldestSale = AddText(86);
        averagePrice = AddText(90);
        averagePrice.AlignmentType = AlignmentType.Right;
        averagePrice.TextFlags = TextFlags.Ellipsis;
        daysAtObservation = AddText(60);
        updatedDaysAgo = AddText(60);
        update = new TextButtonNode
        {
            String = "更新", Size = new(56, 28),
            OnClick = () => { var row = current; row?.Update(row.Id); },
        };
        update.AttachNode(this);
        remove = new TextButtonNode
        {
            String = "删除", Size = new(56, 28),
            OnClick = () => { var row = current; row?.Remove(row.Id); },
        };
        remove.AttachNode(this);
    }

    private TextNode AddText(float width)
    {
        var node = new TextNode { Size = new(width, 36), FontSize = 12, AlignmentType = AlignmentType.Left };
        node.AttachNode(this);
        return node;
    }

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();
        if (name is null || oldestSale is null || averagePrice is null || daysAtObservation is null || updatedDaysAgo is null || update is null || remove is null) return;
        name.Size = new(Math.Max(1, RecycleListColumns.DateX(Width) - 50), 36);
        oldestSale.Position = new(RecycleListColumns.DateX(Width), 0);
        averagePrice.Position = new(RecycleListColumns.PriceX(Width), 0);
        daysAtObservation.Position = new(RecycleListColumns.DaysX(Width), 0);
        updatedDaysAgo.Position = new(RecycleListColumns.AgeX(Width), 0);
        update.Position = new(RecycleListColumns.UpdateX(Width), 4);
        remove.Position = new(RecycleListColumns.DeleteX(Width), 4);
    }

    internal void SetUpdating(bool value)
    {
        if (updating == value) return;
        updating = value;
        update.IsEnabled = !value;
        remove.IsEnabled = !value;
    }

    protected override void SetNodeData(RecycleRow row)
    {
        current = row;
        icon.IconId = row.Icon;
        icon.IsVisible = row.Icon != 0;
        name.String = row.Name;
        oldestSale.String = row.Statistics.OldestSaleDate;
        averagePrice.String = row.Statistics.AverageUnitPrice;
        daysAtObservation.String = Days(row.Statistics.DaysAtObservation);
        updatedDaysAgo.String = Days(row.Statistics.UpdatedDaysAgo);
        TextTooltip = row.Name;
    }

    private static string Days(string value) => value.Length == 0 ? "" : $"{value} 天";
}
