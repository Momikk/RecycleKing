# 构建与开发加载

使用 .NET 10 SDK 和 Dalamud API 15。运行 `./build.ps1`，默认查找 XIVLauncherCN 或 XIVLauncher 的开发程序集，也可通过 `DALAMUD_HOME` 指定。脚本生成 `dist/RecycleKing-0.1.0.1.zip`。官方打包器负责生成插件清单；压缩包由脚本直接写入工作区。

运行 `dotnet run --project tests/RecycleList.Tests.csproj -c Release` 检查清单、品级、拼音排序、背包匹配和可恢复的悬浮框隐藏。测试不运行游戏。

`RecycleKing.GetHighlightedItemIds` 是可选 IPC，返回启用粉色标记时的基础物品 ID 数组；关闭标记时返回空数组。压价之王 0.1.0.21 使用此结果排除蓝框，不要求另一个插件必须安装。AutoRetainer 兼容不依赖 ECommons 或 Dalamud 内部插件管理器，仅访问已加载程序集中的已知悬浮框字段；其依据为 [AutoRetainer 上游代码](https://github.com/PunishXIV/AutoRetainer/blob/master/AutoRetainer/Modules/GcHandin/AutoGCHandin.cs)。

在 Dalamud 设置的开发插件路径中添加 `RecycleKing/bin/Release/RecycleKing.dll`，然后在插件安装器的开发工具页启用。请保留旁边的 `RecycleKing.json`、`RecycleKing.KamiToolKit.dll`、`Assets` 和 `licenses` 文件夹。

正式发布前，完成游戏内验收并创建可下载的版本包；随后将当前版本的双语 Changelog 同步到插件清单和 DalamudPlugins 库链。修改统一库链之前必须重新读取最新文件，保留其他插件的条目。当前没有添加尚不存在的下载链接。
