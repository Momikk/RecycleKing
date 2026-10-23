using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Interfaces;
using KamiToolKit.Nodes;
using Lumina.Data.Parsing.Uld;
using Item = Lumina.Excel.Sheets.Item;

namespace RecycleKing;

internal sealed record RecycleRow(uint Id, uint Icon, string Name, Action<uint> Remove);

internal sealed unsafe class NativeListWindow : NativeAddon
{
    private readonly Plugin plugin;
    private ListNode<RecycleRow, RecycleRowNode>? list;
    private TextNode? count;
    private ResNode? heading;
    private TextButtonNode? clear;
    private TextButtonNode? cancel;
    private int revision = -1;
    private bool confirmClear;
    private bool pendingOpen;

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
        heading = new ResNode { Position = origin, Size = new(width, 28) };
        AddNode(heading);
        foreach (var (label, x, cellWidth) in new[] { ("图标", 0f, 40f), ("物品名称", 44f, width - 156), ("操作", width - 112, 96f) })
        {
            var text = new TextNode
            {
                Position = new(x, 0), Size = new(cellWidth, 28), FontSize = 13,
                AlignmentType = AlignmentType.Left, String = label,
            };
            text.AttachNode(heading);
        }
        list = new ListNode<RecycleRow, RecycleRowNode>
        {
            Position = origin + new Vector2(0, 30), Size = new(width, ContentSize.Y - 102),
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
    }

    private void Refresh()
    {
        if (list is null) return;
        var sheet = Plugin.Data.GetExcelSheet<Item>();
        list.OptionsList = plugin.Config.ItemIds.Select(id => sheet.TryGetRow(id, out var item)
            ? new RecycleRow(id, item.Icon, item.Name.ExtractText(), plugin.Remove)
            : new RecycleRow(id, 0, $"未知物品 #{id}", plugin.Remove)).ToList();
        revision = plugin.Revision;
        UpdateControls();
    }

    private void UpdateControls()
    {
        if (clear is null || cancel is null || count is null) return;
        clear.String = confirmClear ? "确认清空清单" : "清空清单";
        clear.IsEnabled = plugin.Config.ItemIds.Count > 0;
        cancel.IsVisible = confirmClear;
        count.String = confirmClear ? "再次点击确认清空，仅移除清单记录。" : $"共 {plugin.Config.ItemIds.Count} 种物品 · HQ/NQ 合并";
    }

    protected override void OnFinalize(AtkUnitBase* addon)
    {
        list?.Dispose(); list = null;
        heading?.Dispose(); heading = null;
        count?.Dispose(); count = null;
        clear?.Dispose(); clear = null;
        cancel?.Dispose(); cancel = null;
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
        remove = new TextButtonNode
        {
            String = "移出清单", Size = new(90, 28),
            OnClick = () => { var row = current; row?.Remove(row.Id); },
        };
        remove.AttachNode(this);
    }

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();
        if (name is null) return;
        name.Size = new(Math.Max(1, Width - 148), 36);
        remove.Position = new(Width - 96, 4);
    }

    protected override void SetNodeData(RecycleRow row)
    {
        current = row;
        icon.IconId = row.Icon;
        icon.IsVisible = row.Icon != 0;
        name.String = row.Name;
        TextTooltip = row.Name;
    }
}
