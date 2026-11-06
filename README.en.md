# 回收之王 RecycleKing

<p align="center"><img src="images/icon.png" alt="RecycleKing icon" width="128" /></p>

A native “废品回收” button appears at the bottom-left of the inventory during a gil-shop or retainer selling session. It stays visible while the retainer selling list is open and is temporarily disabled during manual sales, context menus, or price editing. Clicking it sells listed items from the four normal bags, including HQ and full stacks. Click again to stop. Open the saved list through plugin settings or `/rking`.

Right-click an inventory item and select “加入回收清单” at the bottom of the menu, marked with a pink boxed K, to register it. HQ and NQ share one entry. Existing entries disable the add action.

The native, scrollable list has columns for icons, names, and removal buttons. A separate clear button requires a second click to confirm. Entries are stored in the plugin configuration and shared by characters using that configuration. Use `/recycleking` or `/rking` to open the list.

The armoury chest gains a bottom-left Garbage Out button. It moves items not used by any saved gearset into free normal-bag slots across all categories, including unreferenced soul crystals. HQ and NQ are checked separately, and every matching copy of saved gear is protected. Click again to stop. Having 5 or fewer free normal-bag slots, closing the armoury chest, changed gearset references or changed source items stop the operation. Newly received items are not added to the captured plan. Selling, desynthesis and armoury cleanup cannot run together. Actual item movement remains unverified in-game.

A native Auto Desynthesis button appears at the top-right of the desynthesis list. Clicking captures the complete current tab, including rows below the scroll area and HQ items, and processes it one item at a time in list order, waiting for native item processing to end before continuing. Click Stop Desynthesis to stop. It never switches tabs or adds newly received or newly listed items to that plan.

Close any manual desynthesis confirmation or result window first and keep at least three bag slots free. Tab changes, moved or changed items, list changes, enabled native bulk desynthesis, unknown confirmations and unconfirmed results stop the operation. For a verified item in the captured plan, the rare/unique warning is acknowledged automatically before waiting for the native button to become enabled. Native bulk desynthesis is never enabled. The normal-quality results option is preserved. Submitted desynthesis may complete after stopping. Offline checks and compilation passed; logging and safe stopping on a manual warning were checked in-game; the continuous-desynthesis fix still needs a full in-game run.

The Grand Company expert delivery list also gains an item-level column beside the item names.

Version **0.1.3.5 is an early testing release**. The list is sorted by Chinese pinyin. Listed bag items receive a pink mask and X, temporarily omitted on slots covered by item tooltips, context menus or sale dialogs, taking priority over UndercutKing 0.1.0.21 blue highlights. Both this marker and AutoRetainer GC overlay hiding are enabled by default and can be switched off at the top of the window. Hiding the AutoRetainer overlay does not stop automatic delivery; compatibility may require updates when AutoRetainer changes.

Ready menus and quantity confirmations are processed immediately to reduce flicker. Native windows may still appear; fully invisible operation is unverified. Each sale still waits for inventory and gil acknowledgement. Batch selling starts only after clicking the button; saved list entries remain afterward. Equipped items, the armoury chest, and retainer inventory are excluded. Unsellable items are skipped. The batch stops on context or item changes and unrecognized confirmation dialogs. Submitted sales may still complete. The inventory button supports all three bag layouts. The context entry applies to inventory item menus, excluding chat links and key items. Selling, UI placement, themes, scaling, reopening, and unloading still require in-game verification.

Add the [Momikk plugin feed](https://raw.githubusercontent.com/Momikk/DalamudPlugins/main/pluginmaster.json) in Dalamud's third-party repository settings, then search for **RecycleKing**. Use `/xllog`, filter the source to `RecycleKing`, and include Information/Warning/Error levels to see operation progress and stop reasons. Native errors during desynthesis are also logged. Include the relevant log when reporting a problem. Report problems through [GitHub Issues](https://github.com/Momikk/RecycleKing/issues).

Read the [English changelog](CHANGELOG.md) or [Chinese changelog](CHANGELOG.zh-CN.md). Licensed under **GNU AGPL v3, AGPL-3.0-only**; see the complete [LICENSE](LICENSE) and [third-party notices](THIRD-PARTY-NOTICES.md).
