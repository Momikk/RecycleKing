using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Interfaces;
using KamiToolKit.Nodes;
using Lumina.Data.Parsing.Uld;
using Item = Lumina.Excel.Sheets.Item;

namespace RecycleKing;

internal sealed record RecycleRow(uint Id, uint Icon, string Name, SaleStatisticsCells Statistics, Action<uint> Remove);

internal static class RecycleListColumns
{
    // KamiToolKit reserves 16 pixels for the list's scroll bar.
    internal static float DateX(float rowWidth) => rowWidth - 280;
    internal static float DaysX(float rowWidth) => rowWidth - 190;
    internal static float AgeX(float rowWidth) => rowWidth - 124;
    internal static float DeleteX(float rowWidth) => rowWidth - 58;
}

internal sealed unsafe class NativeListWindow : NativeAddon
{
    private readonly Plugin plugin;
    private ListNode<RecycleRow, RecycleRowNode>? list;
    private TextNode? count;
    private ResNode? heading;
    private TextButtonNode? clear;
    private TextButtonNode? cancel;
    private CheckboxNode? hideOverlay;
    private CheckboxNode? highlight;
    private int revision = -1;
    private bool confirmClear;
    private bool pendingOpen;
    private long refreshAgeAfter;
    private RecycleSortColumn sortColumn = RecycleSortColumn.Name;
    private bool sortDescending;
    private readonly Dictionary<RecycleSortColumn, TextNode> sortHeaders = new();

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
        base.Close();
    }

    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
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
            ("距统计", RecycleListColumns.DaysX(rowWidth), 60f, RecycleSortColumn.DaysAtObservation),
            ("更新距今", RecycleListColumns.AgeX(rowWidth), 60f, RecycleSortColumn.UpdatedDaysAgo),
            ("操作", RecycleListColumns.DeleteX(rowWidth), 52f, null),
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
        revision = -1;
        Refresh();
    }

    protected override void OnUpdate(AtkUnitBase* addon)
    {
        if (revision != plugin.Revision) { confirmClear = false; Refresh(); }
        else if (!confirmClear && plugin.Config.SaleStatistics.Count > 0 && Environment.TickCount64 >= refreshAgeAfter) Refresh();
    }

    private void Refresh()
    {
        if (list is null) return;
        if (hideOverlay is not null) hideOverlay.IsChecked = plugin.Config.HideAutoRetainerGcOverlay;
        if (highlight is not null) highlight.IsChecked = plugin.Config.HighlightListedItems;
        var sheet = Plugin.Data.GetExcelSheet<Item>();
        var now = DateTimeOffset.UtcNow;
        list.OptionsList = RecycleSort.Sort(plugin.Config.ItemIds.Select(id => sheet.TryGetRow(id, out var item)
            ? new RecycleRow(id, item.Icon, item.Name.ExtractText(), Statistics(id), plugin.Remove)
            : new RecycleRow(id, 0, $"未知物品 #{id}", Statistics(id), plugin.Remove)),
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
        if (clear is null || cancel is null || count is null) return;
        clear.String = confirmClear ? "确认清空清单" : "清空清单";
        clear.IsEnabled = plugin.Config.ItemIds.Count > 0;
        cancel.IsVisible = confirmClear;
        var order = (sortColumn == RecycleSortColumn.Name ? "拼音" : SortLabel(sortColumn)) + (sortDescending ? "降序" : "升序");
        count.String = confirmClear ? "再次点击确认清空，仅移除清单记录。" : $"共 {plugin.Config.ItemIds.Count} 种物品 · {order} · HQ/NQ 合并";
    }

    protected override void OnFinalize(AtkUnitBase* addon)
    {
        list?.Dispose(); list = null;
        heading?.Dispose(); heading = null;
        sortHeaders.Clear();
        count?.Dispose(); count = null;
        clear?.Dispose(); clear = null;
        cancel?.Dispose(); cancel = null;
        hideOverlay?.Dispose(); hideOverlay = null;
        highlight?.Dispose(); highlight = null;
        confirmClear = false;
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
    private readonly TextNode daysAtObservation;
    private readonly TextNode updatedDaysAgo;
    private readonly TextButtonNode remove;
    private RecycleRow? current;

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
        daysAtObservation = AddText(60);
        updatedDaysAgo = AddText(60);
        remove = new TextButtonNode
        {
            String = "删除", Size = new(52, 28),
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
        if (name is null || oldestSale is null || daysAtObservation is null || updatedDaysAgo is null || remove is null) return;
        name.Size = new(Math.Max(1, RecycleListColumns.DateX(Width) - 50), 36);
        oldestSale.Position = new(RecycleListColumns.DateX(Width), 0);
        daysAtObservation.Position = new(RecycleListColumns.DaysX(Width), 0);
        updatedDaysAgo.Position = new(RecycleListColumns.AgeX(Width), 0);
        remove.Position = new(RecycleListColumns.DeleteX(Width), 4);
    }

    protected override void SetNodeData(RecycleRow row)
    {
        current = row;
        icon.IconId = row.Icon;
        icon.IsVisible = row.Icon != 0;
        name.String = row.Name;
        oldestSale.String = row.Statistics.OldestSaleDate;
        daysAtObservation.String = Days(row.Statistics.DaysAtObservation);
        updatedDaysAgo.String = Days(row.Statistics.UpdatedDaysAgo);
        TextTooltip = row.Name;
    }

    private static string Days(string value) => value.Length == 0 ? "" : $"{value} 天";
}
