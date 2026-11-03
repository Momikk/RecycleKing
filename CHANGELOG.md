# Changelog

[中文更新记录](CHANGELOG.zh-CN.md)

## 0.1.3.3 — 2026-11-04

- Confirm rare and unique item warnings for captured items so auto desynthesis can continue.
- Shorten the plugin description.

## 0.1.3.2 — 2026-11-03

- Wait for item processing to finish before continuing desynthesis.
- Log operation progress, native errors and stop reasons in `/xllog`.

## 0.1.3.1 — 2026-11-03

- Fix auto desynthesis getting stuck at the first result window instead of continuing with the next item.
- Remove hover tooltips from Auto Desynthesis and Garbage Out.

## 0.1.3.0 — 2026-11-03

- Add Garbage Out at the bottom-left of the armoury chest to move items not used by any saved gearset into normal bags, across all categories. Stop when bags are full; click again to stop.
- Protect saved gearset items with separate HQ/NQ identities. Stop if gearset references or item state change.
- Correct desynthesis list matching without requiring its display icon to equal the item icon.
- Actual movement and desynthesis still require in-game verification. Submitted operations may complete after stopping.

## 0.1.2.0 — 2026-11-03

- Add a native Auto Desynthesis button to process items listed in the current tab at the time of clicking, one at a time in list order. Click again to stop.
- Stop on tab or list changes and manual warnings; exclude unlisted items. Preserve the normal-quality results option and never enable native bulk desynthesis.
- In-game behavior remains unverified. Submitted desynthesis may finish after stopping.

## 0.1.1.2 — 2026-10-28

- Move Add to recycling list to the bottom of the context menu.

## 0.1.1.1 — 2026-10-24

- Hide pink masks and X marks on bag slots covered by popups.
- Support item tooltips, context menus and sale dialogs.
- In-game appearance remains unverified.

## 0.1.1.0 — 2026-10-23

- Keep recycling visible while the retainer sale list is open.
- Disable it during manual sales or price editing.
- UI behavior awaits in-game verification.

## 0.1.0.5 — 2026-10-23

Speed up batch selling by processing ready menus and quantity confirmations immediately and removing extra per-stack delays to reduce window flicker. Keep inventory and gil checks; wait for game responses. Speed and window behavior remain unverified in-game.

## 0.1.0.4 — 2026-10-23

Fix retainer recycling stopping at the item menu with a native sale-menu verification error. Locate Have Retainer Sell Items using the game label. Show a specific reason if verification fails. Actual selling still requires in-game verification.

## 0.1.0.3 — 2026-10-23

Show recycling during an active retainer interaction without opening retainer inventory. Sell listed items through Have Retainer Sell Items. Stop when dismissing or switching retainers. Retainer selling remains unverified in-game.

## 0.1.0.2 — 2026-10-23

Show the bag recycling button only when selling is available; sell listed normal-bag items, including HQ and full stacks. Click again to stop. Open the list through plugin settings or /rking; keep entries after sales. Stop on context/item changes or unrecognized confirmations; submitted sales may still complete. Selling remains unverified in-game.

## 0.1.0.1 — 2026-10-23

Sort the list by Chinese pinyin. Mark listed bag items with a pink mask and X, ahead of UndercutKing 0.1.0.21 blue highlights. Add an enabled-by-default, reversible AutoRetainer GC overlay hiding option; automatic delivery is unaffected. Overlay compatibility may change with plugin updates; in-game appearance remains unverified.

## 0.1.0.0 — 2026-10-23

Add a native inventory button and a pink K context-menu entry. List icons and names; remove or clear entries and save HQ/NQ together. Show item levels in the expert delivery list. No automatic item processing or delivery; in-game UI verification is pending.
