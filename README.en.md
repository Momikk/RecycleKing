# 回收之王 RecycleKing

<p align="center"><img src="images/icon.png" alt="RecycleKing icon" width="128" /></p>

A native “废品回收” button appears at the bottom-left of the inventory only while selling is available at a gil shop or during an active retainer interaction. Clicking it sells listed items from the four normal bags, including HQ and full stacks. Click again to stop. Open the saved list through plugin settings or `/rking`.

Right-click an inventory item and select “加入回收清单”, marked with a pink boxed K, to register it. HQ and NQ share one entry. Existing entries disable the add action.

The native, scrollable list has columns for icons, names, and removal buttons. A separate clear button requires a second click to confirm. Entries are stored in the plugin configuration and shared by characters using that configuration. Use `/recycleking` or `/rking` to open the list.

The Grand Company expert delivery list also gains an item-level column beside the item names.

Version **0.1.0.3 is an early testing release**. The list is sorted by Chinese pinyin. Listed bag items receive a pink mask and X, taking priority over UndercutKing 0.1.0.31 blue highlights. Both this marker and AutoRetainer GC overlay hiding are enabled by default and can be switched off at the top of the window. Hiding the AutoRetainer overlay does not stop automatic delivery; compatibility may require updates when AutoRetainer changes.

Batch selling starts only after clicking the button; saved list entries remain afterward. Equipped items, the armoury chest, and retainer inventory are excluded. Unsellable items are skipped. The batch stops on context or item changes and unrecognized confirmation dialogs. Submitted sales may still complete. The inventory button supports all three bag layouts. The context entry applies to inventory item menus, excluding chat links and key items. Selling, UI placement, themes, scaling, reopening, and unloading still require in-game verification.

Add the [Momikk plugin feed](https://raw.githubusercontent.com/Momikk/DalamudPlugins/main/pluginmaster.json) in Dalamud's third-party repository settings, then search for **RecycleKing**. Report problems through [GitHub Issues](https://github.com/Momikk/RecycleKing/issues).

Read the [English changelog](CHANGELOG.md) or [Chinese changelog](CHANGELOG.zh-CN.md). Licensed under **GNU AGPL v3, AGPL-3.0-only**; see the complete [LICENSE](LICENSE) and [third-party notices](THIRD-PARTY-NOTICES.md).
