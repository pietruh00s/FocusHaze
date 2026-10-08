## Changed

- **Tidier package.** The top of the `WinDimmer` folder now contains only `WinDimmer.exe` and `LICENSE`. Everything else (DLLs, the .NET runtime, the Windows App SDK and language resources) lives in the `app` subfolder. `WinDimmer.exe` is a tiny launcher that starts `app\WinDimmer.exe`.

## Download

| Package | For |
|---|---|
| `WinDimmer-1.0.2-win-x64.zip` | most PCs (Intel / AMD) |
| `WinDimmer-1.0.2-win-arm64.zip` | ARM PCs (e.g. Snapdragon X) |

Unzip the package anywhere and run `WinDimmer.exe`. You don't need to install .NET or the Windows App SDK.

**Updating from 1.0.x:** close WinDimmer (tray icon → Quit), delete the old folder's contents and extract the new package in its place. Your settings are kept. If **Start with Windows** was on, it keeps working.

> The executable isn't code-signed yet, so Windows SmartScreen may warn you the first time you run it. Choose **More info → Run anyway**.

SHA-256 checksums are in `SHA256SUMS.txt`.
