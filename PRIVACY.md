# FocusHaze Privacy Policy

_Last updated: 11 October 2026_

FocusHaze is a Windows app that dims every window except the active one. This policy explains what data the app handles.

## Summary

**FocusHaze does not collect, transmit, sell or share any personal information.** The app makes no network connections, has no telemetry or analytics, and contains no ads.

## What the app stores on your device

FocusHaze keeps two small files on your own computer, in `%LOCALAPPDATA%\FocusHaze`:

- `settings.json`: your preferences (whether dimming is on, intensity, color, transition settings, display mode and interface language).
- `error.log`: written only if something goes wrong. It contains technical details of the error (time, error message and program call stack, which may include file paths on your computer). It is never sent anywhere and is limited to 256 KB.

If **Start with Windows** is turned on, Windows records that FocusHaze should start when you sign in (through the app's startup task, or for the zip version, a value in your user's registry Run key).

## What the app accesses while running

To dim the right windows, FocusHaze asks Windows which window is active and where windows are on screen (their position, size and window class). It uses this information only in memory to position the dimming overlay. It does not read the contents of your windows, does not take screenshots, and does not record keystrokes. The only keyboard shortcut it listens for is the one that turns dimming on and off (Ctrl+Alt+H).

## Removing your data

- **Microsoft Store version:** uninstalling FocusHaze removes the app and its data.
- **Zip version:** delete the app folder and `%LOCALAPPDATA%\FocusHaze`. If Start with Windows was on, turn it off in the app first (or remove the `FocusHaze` value from `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`).

## Children

FocusHaze does not collect any information from anyone, including children.

## Changes

If this policy changes, the updated version will be published in this file, with a new "Last updated" date.

## Contact

Questions about privacy: open an issue at https://github.com/pietruh00s/FocusHaze/issues.
