<p align="center">
  <img src="images/icon.png" alt="回收之王图标" width="128" />
</p>

# 回收之王 RecycleKing

面向《最终幻想 XIV》物品栏的 Dalamud 插件，整理需要回收的物品清单。

**当前版本为早期测试版（0.1.0.0）**。

[中文更新记录](CHANGELOG.zh-CN.md) · [English Changelog](CHANGELOG.md)

## 当前功能

- 在背包底部左侧增加“回收废品”按钮，点击打开游戏原生清单窗口，支持普通、展开和全展开物品栏。
- 右键物品，选择带粉色 K 前缀的“加入回收清单”进行登记；NQ/HQ 视为同一物品，已加入的物品不能重复登记。
- 清单按列显示物品图标、名称和“移出清单”按钮，支持滚动查看和逐项移出。
- 单独提供“清空清单”按钮，再次确认后清空。清单自动保存，同一插件配置内共享，重启后保留。
- 在大国防联军“筹备稀有品”原生列表中增加“品级”列，便于查看待交纳装备的物品品级。

输入 `/rking` 或 `/recycleking` 可打开窗口。当前版本仅管理清单，不执行出售、丢弃或分解；移出和清空只删除清单记录，不处理背包中的物品。右键入口用于物品库存菜单，不包含聊天链接和任务道具菜单。

## 安装方法

1. 输入 `/xlsettings`，进入 **插件 → 第三方插件**。
2. 添加以下库链，勾选启用并保存：

   ```text
   https://raw.githubusercontent.com/Momikk/DalamudPlugins/main/pluginmaster.json
   ```

3. 输入 `/xlplugins`，搜索 **回收之王** 或 **RecycleKing**，安装并启用。

## 适配与使用条件

需要已启用 Dalamud 的游戏环境。当前版本针对 **Dalamud API 15** 构建，使用国服 XIVLauncherCN 的开发程序集。

已通过编译和清单规则检查。原生按钮位置、滚动、主题与缩放、关闭和卸载表现仍需游戏内验收。其他区服尚未验证；游戏或 Dalamud 大版本更新后，请以插件管理器中的兼容状态为准。

源码构建、检查和开发加载说明见[开发文档](docs/development.md)，当前验证范围见[验收记录](docs/verification.md)。

## 问题反馈

遇到物品登记、清单保存、品级显示或窗口问题，请前往 [GitHub Issues](https://github.com/Momikk/RecycleKing/issues) 提交反馈。

## 项目声明

回收之王是免费、开源的社区第三方插件，与 SQUARE ENIX、盛趣游戏及《最终幻想 XIV》官方无附属关系。

## 开源许可证

Copyright (C) 2026 Momikk。

本项目采用 **GNU Affero General Public License v3.0（AGPL-3.0-only）** 授权，完整条款见 [LICENSE](LICENSE)。

原生 UI 使用 MIT 授权的 KamiToolKit，版权与修改说明见[第三方声明](THIRD-PARTY-NOTICES.md)。

English documentation: [README.en.md](README.en.md).
