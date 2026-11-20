using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Network.Structures;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using NativeObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace RecycleKing;

internal sealed unsafe class MarketRefresh : IDisposable
{
    private const string Board = "ItemSearch";
    private const string Result = "ItemSearchResult";
    private const string History = "ItemHistory";
    private const uint MarketBoardNameRow = 2000442;
    private const float InteractionRange = 4.5f;
    private const long QueryTimeout = 10_000;
    private const long QueryGap = 1_200;
    private static readonly string[] OtherWindows = ["SelectYesno", "SelectOk", "SelectString", "SelectIconString", "Talk",
        "ContextMenu", "ContextIconMenu", "InputNumeric", "RetainerList", "RetainerSell", "RetainerSellList",
        "Shop", "SalvageDialog", "SalvageResult"];

    private readonly Plugin plugin;
    private readonly OperationTrace trace = new("更新成交");
    private readonly object receiptLock = new();
    private (uint Id, string Name)[] queue = [];
    private int cursor;
    private int updated;
    private int skipped;
    private uint territory;
    private uint world;
    private ulong character;
    private ulong boardObjectId;
    private WindowKey boardWindow;
    private WindowKey resultWindow;
    private WindowKey historyWindow;
    private byte resultRequestId;
    private long deadline;
    private long earliestStep;
    private long reopenAfter;
    private int lastRow = -1;
    private bool searchSettled;
    private uint armedItem;
    private MarketHistoryLine[]? receivedRows;
    private SaleStatistics? receipt;
    private bool invalidReceipt;
    private Phase phase;

    private enum Phase { Idle, OpeningBoard, Ready, Searching, OpeningResult, ReadingHistory, ClosingHistory, ClosingResult }
    private readonly record struct WindowKey(nint Address, ushort Id);

    internal bool IsRunning => phase != Phase.Idle;
    internal string Status { get; private set; } = "";

    internal MarketRefresh(Plugin plugin)
    {
        this.plugin = plugin;
        Plugin.Framework.Update += Update;
        Plugin.MarketBoard.HistoryReceived += ReceiveHistory;
    }

    internal bool Start(uint? itemId)
    {
        if (IsRunning || plugin.IsDisposing) return false;
        try
        {
            var player = Plugin.Objects.LocalPlayer;
            if (player == null || !CharacterReady() || OtherOperation() || OtherWindows.Any(Visible)
                || Visible(Result) || Visible(History))
                return Reject("请关闭其他交互窗口后再更新。");
            if (Environment.TickCount64 < reopenAfter) return Reject("上次查询仍在结束，请稍后再试。");

            var sheet = Plugin.Data.GetExcelSheet<Item>();
            var requested = itemId is { } single ? plugin.Config.ItemIds.Where(id => id == single) : plugin.Config.ItemIds;
            var items = new List<(uint Id, string Name)>();
            var unavailable = 0;
            foreach (var id in requested)
            {
                if (!sheet.TryGetRow(id, out var item) || item.IsUntradable || item.IsCollectable || item.ItemSearchCategory.RowId == 0)
                { unavailable++; continue; }
                var name = item.Name.ExtractText();
                if (string.IsNullOrWhiteSpace(name)) { unavailable++; continue; }
                items.Add((id, name));
            }
            if (items.Count == 0) return Reject("没有可在市场查询的物品。");
            var board = FindBoard(player);
            if (board == null) return Reject("请靠近市场布告板后再更新。");
            var currentBoard = FindWindow(Board);
            if (currentBoard == default && (Visible(Board) || Plugin.Conditions[ConditionFlag.OccupiedInEvent]
                || Plugin.Conditions[ConditionFlag.Occupied])) return Reject("请等当前操作结束后再更新。");
            var target = TargetSystem.Instance();
            if (currentBoard == default && target == null) return Reject("游戏交互尚未就绪。");

            queue = items.ToArray();
            cursor = updated = 0;
            skipped = unavailable;
            character = Plugin.Player.ContentId;
            territory = Plugin.ClientState.TerritoryType;
            world = player.CurrentWorld.RowId;
            boardObjectId = board.GameObjectId;
            boardWindow = currentBoard;
            resultWindow = historyWindow = default;
            Status = "正在打开市场布告板…";
            trace.Start($"world={world} items={queue.Length} unavailable={unavailable}");
            SetPhase(currentBoard != default ? Phase.Ready : Phase.OpeningBoard);
            if (currentBoard == default) target->InteractWithObject((NativeObject*)board.Address, true);
            return true;
        }
        catch (Exception error)
        {
            Plugin.Log.Error(error, "回收之王无法开始更新成交记录。");
            if (IsRunning) Finish("无法开始查询，已停止。");
            return Reject("无法开始查询，请查看日志。");
        }
    }

    internal void Stop()
    {
        if (IsRunning) Finish("已停止更新。");
    }

    private void Update(IFramework framework)
    {
        if (!IsRunning) return;
        try { Advance(); }
        catch (Exception error)
        {
            Plugin.Log.Error(error, "回收之王成交查询已停止。");
            Finish("查询出错，已停止。");
        }
    }

    private void Advance()
    {
        var player = Plugin.Objects.LocalPlayer;
        if (player == null || !CharacterReady() || Plugin.Player.ContentId != character || player.CurrentWorld.RowId != world
            || Plugin.ClientState.TerritoryType != territory || OtherOperation() || OtherWindows.Any(Visible)
            || !Plugin.Objects.Any(obj => obj.GameObjectId == boardObjectId && obj.IsTargetable && InRange(player, obj)))
        { Finish("市场查询环境已变化，已停止。"); return; }

        var now = Environment.TickCount64;
        if (now >= deadline)
        {
            if (phase == Phase.Searching && searchSettled && lastRow < 0 && !Visible(Result) && !Visible(History))
            {
                trace.Warning("跳过未找到的物品", $"item={queue[cursor].Id}");
                Next(false);
                return;
            }
            trace.Warning("等待超时", $"phase={phase} item={(cursor < queue.Length ? queue[cursor].Id : 0)}");
            Finish(phase == Phase.OpeningBoard ? "市场布告板未打开，已停止。" : "市场查询未确认，已停止。");
            return;
        }
        if (now < earliestStep) return;

        var board = FindWindow(Board);
        if (phase == Phase.OpeningBoard)
        {
            if (Visible(Result) || Visible(History)) { Finish("出现了其他市场查询，已停止。"); return; }
            if (board == default) return;
            boardWindow = board;
            SetPhase(Phase.Ready);
            return;
        }
        if (board == default || board != boardWindow) { Finish("市场布告板已关闭或切换，已停止。"); return; }

        if (cursor == queue.Length) { Finish("更新完成。"); return; }
        var (itemId, itemName) = queue[cursor];
        var agent = AgentItemSearch.Instance();
        var proxy = InfoProxyItemSearch.Instance();
        if (agent == null || proxy == null || agent->InfoProxyItemSearch != proxy || agent->AddonId != board.Id || !agent->IsAgentActive()) return;

        var result = FindWindow(Result);
        var history = FindWindow(History);
        switch (phase)
        {
            case Phase.Ready:
                if (Visible(Result) || Visible(History)) { Finish("出现了其他市场查询，已停止。"); return; }
                if (agent->IsItemPushPending) return;
                if (!plugin.Config.ItemIds.Contains(itemId)) { Next(false); return; }
                var boardAddon = (AddonItemSearch*)board.Address;
                if (boardAddon->SearchTextInput == null || boardAddon->SearchButton == null) return;
                Status = $"{cursor + 1}/{queue.Length} {itemName}";
                lastRow = -1;
                searchSettled = false;
                SetPhase(Phase.Searching, 350);
                boardAddon->SearchTextInput->SetText(itemName.Length <= 40 ? itemName : itemName[..40]);
                boardAddon->SetModeFilter(AddonItemSearch.SearchMode.Normal, -1);
                boardAddon->RunSearch(false);
                trace.Info("搜索物品", $"item={itemId}");
                break;

            case Phase.Searching:
                if (Visible(Result) || Visible(History)) { Finish("市场搜索已被其他操作接管，已停止。"); return; }
                var list = ((AddonItemSearch*)board.Address)->ResultsList;
                searchSettled = false;
                if (agent->IsItemPushPending || !agent->ListingPageLoaded || list == null) return;
                var count = list->ListLength;
                if (count is < 0 or > 100) return;
                searchSettled = true;
                var row = ExactRow(agent, count, itemId);
                if (row < 0 || list->GetItemDisabledState(row)) { lastRow = -1; return; }
                // Wait for the same exact UI row on a later framework tick after the native search populates it.
                if (lastRow != row) { lastRow = row; earliestStep = now + 100; return; }
                Arm(itemId);
                SetPhase(Phase.OpeningResult);
                list->DispatchItemEvent(row, AtkEventType.ListItemClick);
                trace.Info("打开原生挂牌", $"item={itemId} row={row}");
                break;

            case Phase.OpeningResult:
                if (result == default) return;
                if (!MatchesResult(result, board, agent, proxy, itemId)) { Finish("搜索结果与本次物品不符，已停止。"); return; }
                if (resultWindow != default && resultWindow != result) { Finish("本次查询窗口已切换，已停止。"); return; }
                resultWindow = result;
                if (Visible(History))
                {
                    if (history == default) return;
                    if (!BelongsTo(history, board.Id, result.Id)) { Finish("成交历史窗口归属不符，已停止。"); return; }
                    historyWindow = history;
                    resultRequestId = proxy->CurrentRequestId;
                    SetPhase(Phase.ReadingHistory, 125);
                    break;
                }
                var button = ((AddonItemSearchResult*)result.Address)->History;
                if (button == null || button->AtkResNode == null || !button->IsEnabled || !button->AtkResNode->IsVisible()) return;
                resultRequestId = proxy->CurrentRequestId;
                SetPhase(Phase.ReadingHistory, 125);
                var arg = new AtkValue { Type = AtkValueType.Int, Int = 0 };
                ((AtkUnitBase*)result.Address)->FireCallback(1, &arg, true);
                trace.Info("打开成交历史", $"item={itemId}");
                break;

            case Phase.ReadingHistory:
                if (!OwnResult(result, board, agent, proxy, itemId)) { Finish("本次挂牌窗口已关闭或切换，已停止。"); return; }
                if (history == default)
                {
                    if (historyWindow != default) Finish("本次成交历史已关闭，已停止。");
                    return;
                }
                if (!BelongsTo(history, board.Id, result.Id) || (historyWindow != default && historyWindow != history))
                { Finish("成交历史窗口归属不符，已停止。"); return; }
                historyWindow = history;
                SaleStatistics? data;
                bool invalid;
                int countReceived;
                lock (receiptLock) { data = receipt; invalid = invalidReceipt; countReceived = receivedRows?.Length ?? 0; }
                if (invalid) { Finish("成交历史无法核对，已停止。"); return; }
                if (data == null) return;
                var saved = plugin.SetSaleStatistics(new(itemId, data.OldestSaleAt, data.ObservedAt));
                if (saved) updated++; else skipped++;
                trace.Info("收到成交历史", $"item={itemId} rows={countReceived} saved={saved} oldest={data.OldestSaleAt:O}");
                Disarm();
                SetPhase(Phase.ClosingHistory);
                ((AtkUnitBase*)history.Address)->Close(true);
                break;

            case Phase.ClosingHistory:
                if (Visible(History))
                {
                    if (history != default && history != historyWindow) Finish("成交历史窗口已切换，已停止。");
                    return;
                }
                if (!OwnResult(result, board, agent, proxy, itemId)) { Finish("本次挂牌窗口已关闭或切换，已停止。"); return; }
                SetPhase(Phase.ClosingResult);
                ((AtkUnitBase*)result.Address)->Close(true);
                break;

            case Phase.ClosingResult:
                if (Visible(History)) { Finish("出现了其他成交历史窗口，已停止。"); return; }
                if (Visible(Result))
                {
                    if (result != default && result != resultWindow) Finish("挂牌窗口已切换，已停止。");
                    return;
                }
                Next(true);
                break;
        }
    }

    private static int ExactRow(AgentItemSearch* agent, int count, uint itemId)
    {
        var row = -1;
        for (var i = 0; i < count; i++)
        {
            if (RecycleList.BaseId(agent->ListingPageItemIds[i]) != itemId) continue;
            if (row >= 0) return -1;
            row = i;
        }
        return row;
    }

    private bool OwnResult(WindowKey result, WindowKey board, AgentItemSearch* agent, InfoProxyItemSearch* proxy, uint itemId)
        => result != default && result == resultWindow && proxy->CurrentRequestId == resultRequestId
            && MatchesResult(result, board, agent, proxy, itemId);

    private static bool MatchesResult(WindowKey result, WindowKey board, AgentItemSearch* agent, InfoProxyItemSearch* proxy, uint itemId)
    {
        var addon = (AtkUnitBase*)result.Address;
        if (agent->ResultItemId != itemId || proxy->SearchItemId != itemId || !BelongsTo(result, board.Id)
            || addon->AtkValues == null || addon->AtkValuesCount < 1) return false;
        var value = addon->AtkValues[0];
        return (value.Type == AtkValueType.UInt && value.UInt == itemId)
            || (value.Type == AtkValueType.Int && value.Int == itemId);
    }

    private static bool BelongsTo(WindowKey window, ushort board, ushort result = 0)
    {
        var addon = (AtkUnitBase*)window.Address;
        return Known(addon->ParentId) && Known(addon->HostId) && Known(addon->BlockedParentId);
        bool Known(ushort id) => id == 0 || id == board || id == result;
    }

    private static IGameObject? FindBoard(IGameObject player)
    {
        var names = Plugin.Data.GetExcelSheet<EObjName>();
        var objects = Plugin.Data.GetExcelSheet<EObj>();
        if (!names.TryGetRow(MarketBoardNameRow, out var canonical)) return null;
        var label = canonical.Singular.ExtractText();
        if (string.IsNullOrWhiteSpace(label)) return null;
        return Plugin.Objects.Where(obj => obj is { ObjectKind: ObjectKind.EventObj, IsTargetable: true }
                && obj.Address != 0 && obj.Name.TextValue == label && InRange(player, obj)
                && names.TryGetRow(obj.BaseId, out var name) && name.Singular.ExtractText() == label
                && objects.TryGetRow(obj.BaseId, out var gameObject) && gameObject.Target)
            .MinBy(obj => Vector3.DistanceSquared(player.Position, obj.Position));
    }

    private static bool InRange(IGameObject player, IGameObject board)
        => Vector3.DistanceSquared(player.Position, board.Position) <= InteractionRange * InteractionRange;

    private static bool CharacterReady() => Plugin.Player.IsLoaded && Plugin.Player.ContentId != 0
        && Plugin.Objects.LocalPlayer?.CurrentWorld.RowId > 0 && !Plugin.Conditions[ConditionFlag.BetweenAreas]
        && !Plugin.Conditions[ConditionFlag.BetweenAreas51] && !Plugin.Conditions[ConditionFlag.InCombat]
        && !Plugin.Conditions[ConditionFlag.Casting] && !Plugin.Conditions[ConditionFlag.WatchingCutscene]
        && !Plugin.Conditions[ConditionFlag.OccupiedInQuestEvent] && !Plugin.Conditions[ConditionFlag.OccupiedSummoningBell];

    private bool OtherOperation() => plugin.Sales.IsRunning || plugin.Desynthesis.IsRunning || plugin.Armoury.IsRunning;

    private static WindowKey FindWindow(string name)
    {
        var addon = Plugin.GameGui.GetAddonByName(name);
        return addon.IsNull || !addon.IsReady || !addon.IsVisible ? default : new(addon.Address, addon.Id);
    }

    private static bool Visible(string name) => Plugin.GameGui.GetAddonByName(name).IsVisible;

    private void SetPhase(Phase next, long delay = 0)
    {
        phase = next;
        var now = Environment.TickCount64;
        earliestStep = now + delay;
        deadline = now + QueryTimeout + delay;
    }

    private void Next(bool fromQuery)
    {
        if (!fromQuery) skipped++;
        cursor++;
        resultWindow = historyWindow = default;
        SetPhase(Phase.Ready, fromQuery ? QueryGap : 0);
    }

    private void Arm(uint id)
    {
        lock (receiptLock)
        {
            armedItem = id;
            receivedRows = null;
            receipt = null;
            invalidReceipt = false;
        }
    }

    private void Disarm() => Arm(0);

    private void ReceiveHistory(IMarketBoardHistory response)
    {
        lock (receiptLock)
        {
            if (armedItem == 0 || response.ItemId != armedItem || invalidReceipt) return;
            try
            {
                var rows = response.HistoryListings.Select(row => new MarketHistoryLine(row.PurchaseTime, row.Quantity, row.SalePrice)).ToArray();
                if (!MarketHistory.TrySummarize(armedItem, response.ItemId, rows, DateTimeOffset.UtcNow, out var result)
                    || (receivedRows != null && !receivedRows.SequenceEqual(rows)))
                { invalidReceipt = true; return; }
                receivedRows = rows;
                receipt ??= result;
            }
            catch (Exception error)
            {
                invalidReceipt = true;
                Plugin.Log.Warning(error, "读取原生成交历史失败。");
            }
        }
    }

    private bool Reject(string reason) { Status = reason; return false; }

    private void Finish(string reason)
    {
        if (phase is Phase.OpeningResult or Phase.ReadingHistory) reopenAfter = Environment.TickCount64 + QueryTimeout;
        Disarm();
        phase = Phase.Idle;
        queue = [];
        Status = $"{reason} 已更新 {updated} 项" + (skipped > 0 ? $"，跳过 {skipped} 项" : "");
        trace.Info("结束", $"{Status} world={world}");
        Plugin.Chat.Print($"[回收之王] {Status}");
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= Update;
        Plugin.MarketBoard.HistoryReceived -= ReceiveHistory;
        Disarm();
        phase = Phase.Idle;
        queue = [];
    }
}
