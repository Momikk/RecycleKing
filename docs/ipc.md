# 回收之王 IPC

回收之王 0.1.4.0 起提供以下 Dalamud IPC。使用 `IDalamudPluginInterface.GetIpcSubscriber<参数类型, 返回类型>` 注册调用方，无参数时只写返回类型。所有名称加 `RecycleKing.` 前缀。

| 名称 | 参数 | 返回 | 行为 |
| --- | --- | --- | --- |
| `GetApiVersion` | 无 | `int` | 当前接口版本为 `1`。 |
| `GetItemIds` | 无 | `Task<uint[]>` | 读取已保存的完整回收清单，返回基础物品 ID。 |
| `GetHighlightedItemIds` | 无 | `uint[]` | 保留原接口。关闭粉色标记时返回空数组。 |
| `AddItem` | `uint itemId` | `Task<bool>` | 添加有效物品，HQ 和 NQ 归并。已有或无效 ID 返回 `false`。 |
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
