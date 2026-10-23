# Third-party notices

RecycleKing includes a source snapshot of [KamiToolKit](https://github.com/Dalamud-DailyRoutines/KamiToolKit), originally by [MidoriKami](https://github.com/MidoriKami/KamiToolKit), under the MIT license. The snapshot was copied from this workspace's SuitKing dependency, based on upstream commit `e6f316bcb06da25020f2d300345b339789fa7efb`.

The complete copyright and MIT license are preserved in [vendor/KamiToolKit/LICENSE](vendor/KamiToolKit/LICENSE) and included in the package as `licenses/KamiToolKit-MIT.txt`. RecycleKing itself is AGPL-3.0-only.

Local changes inherited from SuitKing: API 15 build settings, nullable annotations, ReadOnlySeString compatibility changes, and a NativeAddon title/subtitle lifecycle fix. This project changes the dependency assembly name to `RecycleKing.KamiToolKit` to isolate its static state from other plugins. No Daily Routines module code is bundled.

Batch selling is an independent implementation using the native interfaces documented in [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs). Context-menu, quantity-dialog, and shop transaction layouts were checked against FFXIVClientStructs; the workflow was also compared with the publicly available AutoRetainer inventory-management and ECommons UI helpers. Neither project is a bundled runtime dependency, and no hooks or signature-scanned sale delegates are copied from them.
