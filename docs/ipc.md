# 回收之王 IPC

回收之王 0.1.4.0 起提供以下 Dalamud IPC。使用 `IDalamudPluginInterface.GetIpcSubscriber<参数类型, 返回类型>` 注册调用方，无参数时只写返回类型。所有名称加 `RecycleKing.` 前缀。

| 名称 | 参数 | 返回 | 行为 |
| --- | --- | --- | --- |
| `GetApiVersion` | 无 | `int` | 当前接口版本为 `1`。 |
| `GetItemIds` | 无 | `Task<uint[]>` | 读取已保存的完整回收清单，返回基础物品 ID。 |
| `GetHighlightedItemIds` | 无 | `uint[]` | 保留原接口。关闭粉色标记时返回空数组。 |
| `AddItem` | `uint itemId` | `Task<bool>` | 添加有效物品，HQ 和 NQ 归并。已有或无效 ID 返回 `false`。 |
| `AddItemWithSaleStatistics` | `string json` | `Task<bool>` | 0.1.5.0 起，一次调用加入物品并保存成交统计；已有物品更新统计。 |
| `SetSaleStatistics` | `string json` | `Task<bool>` | 0.1.5.0 起，更新已在清单中的物品统计，不添加物品。 |
| `GetSaleStatistics` | 无 | `Task<string>` | 0.1.5.0 起，返回已保存统计的 JSON 数组。 |
| `RemoveItem` | `uint itemId` | `Task<bool>` | 移除物品，接受基础或 HQ 编码。未登记时返回 `false`。 |
| `ClearItems` | 无 | `Task<bool>` | 清空回收清单。清单原本为空时返回 `false`。 |
| `SetListOpen` | `bool open` | `Task<bool>` | 请求打开或关闭清单窗口。通过 `GetStatus` 确认实际显示。 |
| `SetHighlightListedItems` | `bool enabled` | `Task<bool>` | 保存背包粉色标记开关。 |
| `SetHideAutoRetainerGcOverlay` | `bool enabled` | `Task<bool>` | 保存隐藏 AutoRetainer 军票悬浮框的开关。 |
| `SetShowExpertDeliveryLevels` | `bool enabled` | `Task<bool>` | 保存稀有品品级列开关。 |
| `StartBatchSale` / `StopBatchSale` | 无 | `Task<bool>` | 启动或停止「废品回收」。 |
| `StartBatchDesynthesis` / `StopBatchDesynthesis` | 无 | `Task<bool>` | 启动或停止当前原生标签的「自动分解」。 |
| `StartArmouryCleanup` / `StopArmouryCleanup` | 无 | `Task<bool>` | 启动或停止「垃圾出库」，保留套装装备及 5 个普通背包空格。 |
| `GetStatus` | 无 | `Task<string>` | 返回 JSON 状态快照。 |

批处理的 `Start…` 返回 `true` 表示已启动。游戏条件不足、无待处理项目或该操作已在运行时返回 `false`。`Stop…` 只停止指定操作，已提交的游戏动作仍可能完成。启动沿用游戏按钮的条件，需要先打开相应的窗口。废品回收还需要打开普通背包。三种批处理互斥。停止时仍待核对的游戏动作可能暂时阻止同类操作重新启动。

`GetStatus` 包含 `listOpen`、`highlightListedItems`、`hideAutoRetainerGcOverlay` 和 `showExpertDeliveryLevels`。`batchSale`、`batchDesynthesis`、`armouryCleanup` 各包含 `running`、`confirmedItems` 和 `message`。`batchSale` 另含 `confirmedStacks`。计数来自当前或最近一次成功启动的批次。`message` 是供人阅读的中文提示，调用方可以用布尔值判断是否已启动或正在运行。

0.1.4.3 起，`batchDesynthesis` 另含整数 `outputRows`：原批全部完成后，当前标签页中保留的已核对分解产物行数。它不是产物件数或待处理数；默认值为 `0`，每次成功启动归零，只有正常完成时才赋值。原有“当前标签页已分解完成。”提示保持不变，API 版本仍为 `1`。兼容旧版时将缺失字段视为 `0`。调用方应结合本次成功启动、`running=false`、原批确认件数和正常完成状态判断结束，不要仅凭 `outputRows=0` 判定完成。

Since 0.1.4.3, `batchDesynthesis.outputRows` is an optional integer counting verified output rows left in the current tab after the original batch completes. It defaults to zero, resets on each accepted start, and is assigned only on normal completion. It is not an item-unit or pending-work count. The existing completion message and API version `1` are unchanged. Treat a missing field as zero for older versions, and combine it with the accepted run, running state, confirmed original count and successful completion; zero alone does not prove completion.

### 成交统计（0.1.5.0）

`AddItemWithSaleStatistics` 和 `SetSaleStatistics` 接受同一 JSON 对象：`itemId` 为物品 ID（接受 HQ 编码，按基础 ID 保存）；`oldestSaleAt` 为这次统计实际读到的最早成交时间；`observedAt` 为实际统计时间。两个时间使用带时区的 ISO 8601 字符串，也可为 `null`，不得用调用或保存时间冒充缺失的统计时间。没有统计时三个显示值留空；只有一个时间时仅显示能算出的单元格。日期按本机时区显示，天数取完整的 24 小时，不按日期跨零点计天。第二列用 `observedAt - oldestSaleAt`，第三列用当前时间减 `observedAt`，不改变原始统计时间。

输入必须是单个对象，最长 2048 字符。拒绝无效物品、未来时间、UnixEpoch 前的默认时间、最早成交晚于统计时间，以及用更旧或缺失的统计时间覆盖已有观测。同一统计时间内容冲突时拒绝，重复的相同快照返回 `true`；更新的统计可以替换旧记录。`AddItemWithSaleStatistics` 先核对全部数据，再一次保存物品和统计，`SetSaleStatistics` 对未登记物品返回 `false`。删除物品或清空清单时一并删除统计，旧 `AddItem` 入口仍可使用。接口版本仍为 `1`，旧插件可能没有这些新端点；调用方应处理 `IpcNotReadyError`，必要时退回旧 `AddItem`，不要因此出售或丢弃物品。

`GetSaleStatistics` 返回 `[{"itemId":5187,"oldestSaleAt":"2026-07-01T08:00:00+00:00","observedAt":"2026-11-19T14:00:00+00:00"}]` 这样的数组，只含已保存的统计；没有条目的物品表示未知。清单内 HQ/NQ 共用一份最近收到的统计，调用方负责当前世界与查询范围的归属。本插件不主动联网取历史，也不将“本次读到的最早一笔”当成物品的终身最早成交。

```csharp
using System.Text.Json;

var add = pluginInterface.GetIpcSubscriber<string, Task<bool>>("RecycleKing.AddItemWithSaleStatistics");
var json = JsonSerializer.Serialize(new { itemId, oldestSaleAt, observedAt });
bool accepted = await add.InvokeFunc(json);
// 已有条目同样可更新；false 表示本次数据未应用，不能据此判定物品已处理。
```

Since 0.1.5.0, `AddItemWithSaleStatistics(string)` and `SetSaleStatistics(string)` return `Task<bool>` and accept `{itemId, oldestSaleAt, observedAt}` JSON. Timestamps are nullable ISO 8601 values with UTC offsets. The first call adds or updates atomically; the second requires an existing list entry. Missing data stays blank. Future, reversed, uninitialized, stale or conflicting observations are rejected. Identical snapshots are idempotent. `GetSaleStatistics()` returns `Task<string>` containing saved records as a JSON array. HQ/NQ share one base-item record; deletion removes its statistics. The provider does not fetch history or invent observation dates. API version remains `1`; callers must handle missing endpoints on older versions.

使用 `await` 等待接口返回，尤其不要在游戏线程用 `.Result` 或 `.Wait()`。插件未加载时 IPC 可能抛出 `IpcNotReadyError`，卸载期间已排队的调用也可能失败。调用启动接口前应确认调用方自己的触发条件。开始、停止和查询示例：

```csharp
using System.Text.Json;

var start = pluginInterface.GetIpcSubscriber<Task<bool>>("RecycleKing.StartArmouryCleanup");
var status = pluginInterface.GetIpcSubscriber<Task<string>>("RecycleKing.GetStatus");
var started = await start.InvokeFunc();
using var snapshot = JsonDocument.Parse(await status.InvokeFunc());
bool running = snapshot.RootElement.GetProperty("armouryCleanup").GetProperty("running").GetBoolean();

// 在调用方收到停止指令时使用。
var stop = pluginInterface.GetIpcSubscriber<Task<bool>>("RecycleKing.StopArmouryCleanup");
// await stop.InvokeFunc();
```
