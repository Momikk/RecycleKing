# 构建与开发加载

使用 .NET 10 SDK 和 Dalamud API 15。运行 `./build.ps1`，默认查找 XIVLauncherCN 或 XIVLauncher 的开发程序集，也可通过 `DALAMUD_HOME` 指定。脚本生成 `dist/RecycleKing-0.1.0.0.zip`。官方打包器负责生成插件清单；压缩包由脚本直接写入工作区。

运行 `dotnet run --project tests/RecycleList.Tests.csproj -c Release` 检查 HQ/NQ 去重、不同物品隔离、无效 ID 排除、列表序列化往返及单项移出。测试不运行游戏。

在 Dalamud 设置的开发插件路径中添加 `RecycleKing/bin/Release/RecycleKing.dll`，然后在插件安装器的开发工具页启用。请保留旁边的 `RecycleKing.json`、`RecycleKing.KamiToolKit.dll`、`Assets` 和 `licenses` 文件夹。

正式发布前，完成游戏内验收并创建可下载的版本包；随后将当前版本的双语 Changelog 同步到插件清单和 DalamudPlugins 库链。修改统一库链之前必须重新读取最新文件，保留其他插件的条目。当前没有添加尚不存在的下载链接。
