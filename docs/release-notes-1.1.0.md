## WinDimmer is now FocusHaze

The app has a new name, **FocusHaze**, because "WinDimmer" is already taken in the Microsoft Store. Nothing changes in how it works.

- The packages are now called `FocusHaze-win-x64.zip` / `FocusHaze-win-arm64.zip`, and the app is started with `FocusHaze.exe`.
- **Your settings carry over.** On first launch FocusHaze reads your WinDimmer settings, and if **Start with Windows** was on, it now starts FocusHaze instead.

## Download

| Package | For |
|---|---|
| `FocusHaze-win-x64.zip` | most PCs (Intel / AMD) |
| `FocusHaze-win-arm64.zip` | ARM PCs (e.g. Snapdragon X) |

Unzip the package anywhere and run `FocusHaze.exe`. You don't need to install .NET or the Windows App SDK.

**Updating from WinDimmer:** quit WinDimmer first (tray icon → Quit), extract FocusHaze, run it once, then delete the old WinDimmer folder.

> The executable isn't code-signed yet, so Windows SmartScreen may warn you the first time you run it. Choose **More info → Run anyway**.

SHA-256 checksums are in `SHA256SUMS.txt`.
