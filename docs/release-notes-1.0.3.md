## Fixed

- **Smooth transitions no longer flash the whole screen.** When you switch windows, only the window that just lost focus fades into the haze; the rest of the screen stays dimmed the whole time. Full-screen fades are still used where nothing was dimmed before: turning dimming on, coming back from the desktop, or moving to another display with **Only the display with the active window** enabled.

## Download

| Package | For |
|---|---|
| `WinDimmer-win-x64.zip` | most PCs (Intel / AMD) |
| `WinDimmer-win-arm64.zip` | ARM PCs (e.g. Snapdragon X) |

Unzip the package anywhere and run `WinDimmer.exe`. You don't need to install .NET or the Windows App SDK.

**Updating:** close WinDimmer (tray icon → Quit), delete the old folder's contents and extract the new package in its place. Your settings are kept.

> The executable isn't code-signed yet, so Windows SmartScreen may warn you the first time you run it. Choose **More info → Run anyway**.

SHA-256 checksums are in `SHA256SUMS.txt`.