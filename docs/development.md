# 构建与开发加载

使用 .NET 10 SDK 和 Dalamud API 15。运行 `./build.ps1`，默认查找 XIVLauncherCN 或 XIVLauncher 的开发程序集，也可通过 `DALAMUD_HOME` 指定。脚本生成 `dist/RecycleKing-0.1.0.2.zip`。官方打包器负责生成插件清单；压缩包由脚本直接写入工作区。

运行 `dotnet run --project tests/RecycleList.Tests.csproj -c Release` 检查清单、品级、拼音排序、背包匹配、可恢复的悬浮框隐藏和批量出售归属/确认规则。测试不运行游戏。

`RecycleKing.GetHighlightedItemIds` 是可选 IPC，返回启用粉色标记时的基础物品 ID 数组；关闭标记时返回空数组。压价之王 0.1.0.21 使用此结果排除蓝框，不要求另一个插件必须安装。AutoRetainer 兼容不依赖 ECommons 或 Dalamud 内部插件管理器，仅访问已加载程序集中的已知悬浮框字段；其依据为 [AutoRetainer 上游代码](https://github.com/PunishXIV/AutoRetainer/blob/master/AutoRetainer/Modules/GcHandin/AutoGCHandin.cs)。

在 Dalamud 设置的开发插件路径中添加 `RecycleKing/bin/Release/RecycleKing.dll`，然后在插件安装器的开发工具页启用。请保留旁边的 `RecycleKing.json`、`RecycleKing.KamiToolKit.dll`、`Assets` 和 `licenses` 文件夹。

发布时记录实际游戏内验收范围并创建可下载的版本包；将当前版本的双语 Changelog 同步到插件清单和 DalamudPlugins 库链。用户选择不在游戏内运行时，将未验证范围写入说明。修改统一库链之前必须重新读取最新文件，保留其他插件的条目。

批量出售通过 `AgentInventoryContext.OpenForItemSlot` 打开物品菜单，核对来源库存和原生出售接收者，再选择原生出售项。普通金币商店支持纯文本原生 Sell 菜单回退；雇员只接受接收者匹配且 CallbackParam=5 的交店出售项，不选择市场上架。数量窗口的最大值必须等于捕获的整组数量；确认窗口必须处于本次物品、库存和槽位的原生出售事务中，且包含该物品名称。库存总量恰好减少本组数量且金币增长足够后才确认完成。出现未知确认框或超时不重试本组。
