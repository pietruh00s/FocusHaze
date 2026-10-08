<p align="center">
  <img src="docs/banner.png" alt="WinDimmer – HazeOver-style focus tool for Windows 11" width="100%">
</p>

<p align="center">
  <a href="https://github.com/pietruh00s/WinDimmer/releases/download/v1.0.2/WinDimmer-1.0.2-win-x64.zip"><img src="https://img.shields.io/badge/Download-x64-0078D4?style=for-the-badge" alt="Download x64"></a>
  &nbsp;
  <a href="https://github.com/pietruh00s/WinDimmer/releases/download/v1.0.2/WinDimmer-1.0.2-win-arm64.zip"><img src="https://img.shields.io/badge/Download-ARM64-0078D4?style=for-the-badge" alt="Download ARM64"></a>
</p>

<p align="center">
  <a href="https://github.com/pietruh00s/WinDimmer/releases/latest"><img src="https://img.shields.io/github/v/release/pietruh00s/WinDimmer" alt="Latest release"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/pietruh00s/WinDimmer" alt="License: MIT"></a>
  <a href="https://buymeacoffee.com/piotrosika"><img src="https://img.shields.io/badge/Buy%20Me%20a%20Coffee-ffdd00?logo=buymeacoffee&logoColor=black" alt="Buy Me a Coffee"></a>
</p>

<p align="center"><b>English</b> | <a href="README.pl.md">Polski</a></p>

A [HazeOver](https://hazeover.com/)-style focus tool for Windows 11, built with WinUI 3. WinDimmer dims every window except the one you're working in, so the rest of the screen fades into the background.

<p align="center">
  <img src="docs/screenshot.png" alt="WinDimmer settings window" width="480">
</p>

## Download

Get the latest version from [**Releases**](https://github.com/pietruh00s/WinDimmer/releases/latest):

- `WinDimmer-<version>-win-x64.zip`: most PCs (Intel / AMD)
- `WinDimmer-<version>-win-arm64.zip`: ARM PCs (e.g. Snapdragon X)

Unzip the package anywhere and run `WinDimmer.exe`. You don't need to install .NET or the Windows App SDK, because everything is included.

> The executable isn't code-signed yet, so Windows SmartScreen may warn you the first time you run it. Choose **More info → Run anyway**.

## Features

- Translucent haze under the active window; clicks go through it to the windows below
- Adjustable intensity and haze color
- Smooth fade when you switch windows, with an adjustable duration
- Dim all displays, or only the display with the active window
- System tray icon: left-click opens settings, right-click opens a menu
- Global hotkey **Ctrl+Alt+H** to turn dimming on or off
- Optional start with Windows (straight to the tray)
- Single instance: launching it again opens the running app's settings
- 14 UI languages, switchable on the fly: English, Polish, German, French, Spanish, Italian, Portuguese, Dutch, Ukrainian, Russian, Turkish, Japanese, Korean and Simplified Chinese. The app follows your Windows display language by default.

## Requirements

- Windows 10 version 2004 (build 19041) or later; Windows 11 recommended
- To build from source: .NET 10 SDK

## Building

```bash
dotnet build -c Release
```

```bash
dotnet run
```

The app is unpackaged and bundles the Windows App SDK, so the build output folder can be copied anywhere. Pass `-p:Platform=ARM64` to build for ARM64.

To build the release packages (x64 and ARM64 zips plus `SHA256SUMS.txt`):

```bash
pwsh ./build-release.ps1
```

The packages are written to `artifacts\`. Each zip contains a `WinDimmer` folder with only `WinDimmer.exe` and `LICENSE` at the top level: the exe is a tiny .NET Framework 4.8 launcher (`Launcher/`, built into every supported Windows) that starts the real app from the `app\` subfolder, which holds everything else. See [CHANGELOG.md](CHANGELOG.md) for release history.

Settings are stored in `%LOCALAPPDATA%\WinDimmer\settings.json`.

## How it works

- `Core/DimOverlay.cs`: a native Win32 window (`WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE`) filled with a solid color, with its opacity set through `SetLayeredWindowAttributes`.
- `Core/DimController.cs`: uses `SetWinEventHook` to watch foreground changes, minimizing, closing and hiding of windows, then calls `SetWindowPos` to place the haze in the Z-order **directly beneath** the active window, so everything behind it is dimmed. The haze disappears when the desktop is focused. The taskbar, the Alt+Tab switcher and the Start menu don't affect it.
- `Core/TrayHost.cs`: a hidden window that owns the tray icon, its context menu, the global hotkey and the signal from a second instance.
- `MainWindow.xaml`: the WinUI 3 settings window (Mica, custom title bar).

## Translations

UI strings live in `Localization/Strings.resx` (English, the fallback) and `Localization/Strings.<code>.resx`. To add a language:

1. Copy `Strings.resx` to `Strings.<code>.resx` (e.g. `Strings.cs.resx`) and translate the values.
2. Add the language code to `Loc.SupportedLanguages` in `Localization/Loc.cs`.

In XAML, bind text with `loc:Localize.Key="KeyName"`; in code, use `Loc.Get("KeyName")`.

Improvements to the existing translations are welcome too.

## Support

If WinDimmer helps you focus, you can buy me a coffee ☕

<a href="https://buymeacoffee.com/piotrosika"><img src="https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png" alt="Buy Me a Coffee" height="48"></a>

## License

[MIT](LICENSE)
