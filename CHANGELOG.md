# Changelog

[中文更新记录](CHANGELOG.zh-CN.md)

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
