# Partner Center submission notes

## Notes for certification (paste into Submission › Notes for certification)

FocusHaze dims every window except the active one, to help users focus.

How to test:
1. Launch FocusHaze. The settings window opens and dimming is on: all other windows are covered by a translucent dark overlay.
2. Click any other window: it becomes active and is shown at full brightness, while the window you left fades into the overlay. Clicks pass through the overlay.
3. Press Ctrl+Alt+H (or use the tray icon menu) to turn dimming off and on.
4. Close the settings window: the app keeps running in the notification area. Right-click the tray icon › Quit to exit.
No account, sign-in or network connection is needed.

Technical notes:
- runFullTrust: FocusHaze is a WinUI 3 desktop app. It shows its overlay as a click-through layered window (WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE) that it positions directly below the foreground window with SetWindowPos.
- To know which window is active it uses SetWinEventHook (WINEVENT_OUTOFCONTEXT) for EVENT_SYSTEM_FOREGROUND, minimize, move/size-end, destroy, hide and cloak events, which is the documented way to observe foreground changes. It reads only window handles, class names, visibility and bounds; it never reads window contents, injects into other processes or uses UI Automation to control other apps.
- The global shortcut is registered with RegisterHotKey (Ctrl+Alt+H).
- "Start with Windows" uses the package's StartupTask (windows.startupTask), off by default and turned on only by the user in the app's settings.
- The app does not change any Windows settings, makes no network connections and collects no data. Privacy policy: https://github.com/pietruh00s/FocusHaze/blob/main/PRIVACY.md

## Restricted capability justification (runFullTrust)

FocusHaze is a desktop (Win32/WinUI 3) app. Full trust is required to create the click-through overlay window, keep it in the z-order below the foreground window and observe foreground-window changes system-wide, which is the app's only function.

## Other submission fields

- **Category:** Productivity (subcategory: none)
- **Age rating:** complete the IARC questionnaire; the app has no user-generated content, no communication between users, no purchases and no location access.
- **Privacy policy URL:** https://github.com/pietruh00s/FocusHaze/blob/main/PRIVACY.md
- **Website:** https://github.com/pietruh00s/FocusHaze
- **Support contact:** https://github.com/pietruh00s/FocusHaze/issues
- **Package:** the `.msixbundle` (x64 + ARM64) uploaded under Packages.
- **Store listings and screenshots:** see [listings.md](listings.md) and `screenshot-*.png` in this folder.
