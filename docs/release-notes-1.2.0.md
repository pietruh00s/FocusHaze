## Added

- **Microsoft Store edition on the way.** FocusHaze has been prepared for the Microsoft Store: an MSIX package, a privacy policy ([PRIVACY.md](https://github.com/pietruh00s/FocusHaze/blob/main/PRIVACY.md)) and Store listings in all 14 languages. The zip below stays available as before.
- **More robust.** Unexpected errors are written to a local `%LOCALAPPDATA%\FocusHaze\error.log` instead of closing the app. Nothing is ever sent anywhere.

## Download

| Package | For |
|---|---|
| `FocusHaze-win-x64.zip` | most PCs (Intel / AMD) |
| `FocusHaze-win-arm64.zip` | ARM PCs (e.g. Snapdragon X) |

Unzip the package anywhere and run `FocusHaze.exe`. You don't need to install .NET or the Windows App SDK.

**Updating:** quit FocusHaze (tray icon → Quit), delete the old folder's contents and extract the new package in its place. Your settings are kept. Coming from WinDimmer? Quit it first; FocusHaze takes over its settings and **Start with Windows** on first launch.

> The executable isn't code-signed yet, so Windows SmartScreen may warn you the first time you run it. Choose **More info → Run anyway**.

SHA-256 checksums are in `SHA256SUMS.txt`.
