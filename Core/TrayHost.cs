using System.Runtime.InteropServices;
using WinDimmer.Localization;
using WinDimmer.Native;

namespace WinDimmer.Core;

/// <summary>
/// Hidden window that owns the notification-area icon, its context menu and the global hotkey.
/// A second app instance finds it by class name and asks it to show the settings window.
/// </summary>
internal sealed class TrayHost : IDisposable
{
    public const string ClassName = "WinDimmer.TrayHost";
    public const uint WM_SHOW_SETTINGS = Win32.WM_APP + 2;
    public const string HotkeyDisplayName = "Ctrl+Alt+H";

    private const uint WM_TRAYICON = Win32.WM_APP + 1;
    private const uint IconId = 1;
    private const int HotkeyId = 1;
    private const uint HotkeyVirtualKey = 'H';

    private const uint CmdSettings = 1;
    private const uint CmdToggle = 2;
    private const uint CmdExit = 3;

    private readonly Win32.WndProc _wndProc;
    private readonly IntPtr _hwnd;
    private readonly IntPtr _icon;
    private readonly bool _ownsIcon;
    private readonly uint _taskbarCreatedMessage;
    private bool _enabled;
    private bool _disposed;

    public event Action? OpenSettingsRequested;
    public event Action? ToggleRequested;
    public event Action? ExitRequested;

    public IntPtr Handle => _hwnd;
    public bool HotkeyRegistered { get; }

    public TrayHost(bool enabled)
    {
        _enabled = enabled;
        _wndProc = WndProc;
        _hwnd = Win32.CreateNativeWindow(ClassName, "WinDimmer", 0, 0, _wndProc);
        _taskbarCreatedMessage = Win32.RegisterWindowMessage("TaskbarCreated");
        (_icon, _ownsIcon) = LoadIcon();

        AddIcon();
        HotkeyRegistered = Win32.RegisterHotKey(_hwnd, HotkeyId,
            Win32.MOD_CONTROL | Win32.MOD_ALT | Win32.MOD_NOREPEAT, HotkeyVirtualKey);
    }

    public void UpdateState(bool enabled)
    {
        _enabled = enabled;
        var data = CreateIconData(Win32.NIF_TIP);
        Win32.Shell_NotifyIcon(Win32.NIM_MODIFY, ref data);
    }

    private static (IntPtr Icon, bool Owned) LoadIcon()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "WinDimmer.ico");
        IntPtr icon = Win32.LoadImage(IntPtr.Zero, path, Win32.IMAGE_ICON,
            Win32.GetSystemMetrics(Win32.SM_CXSMICON), Win32.GetSystemMetrics(Win32.SM_CYSMICON), Win32.LR_LOADFROMFILE);
        return icon != IntPtr.Zero
            ? (icon, true)
            : (Win32.LoadIcon(IntPtr.Zero, Win32.IDI_APPLICATION), false);
    }

    private Win32.NOTIFYICONDATA CreateIconData(uint flags) => new()
    {
        cbSize = (uint)Marshal.SizeOf<Win32.NOTIFYICONDATA>(),
        hWnd = _hwnd,
        uID = IconId,
        uFlags = flags,
        uCallbackMessage = WM_TRAYICON,
        hIcon = _icon,
        szTip = Loc.Get(_enabled ? "Tray_TooltipOn" : "Tray_TooltipOff"),
        szInfo = "",
        szInfoTitle = "",
    };

    private void AddIcon()
    {
        var data = CreateIconData(Win32.NIF_MESSAGE | Win32.NIF_ICON | Win32.NIF_TIP);
        Win32.Shell_NotifyIcon(Win32.NIM_ADD, ref data);
    }

    private void ShowContextMenu()
    {
        IntPtr menu = Win32.CreatePopupMenu();
        Win32.AppendMenu(menu, Win32.MF_STRING, CmdSettings, Loc.Get("Tray_Settings"));
        Win32.AppendMenu(menu, Win32.MF_STRING | (_enabled ? Win32.MF_CHECKED : 0), CmdToggle,
            $"{Loc.Get("Tray_Dimming")}\t{HotkeyDisplayName}");
        Win32.AppendMenu(menu, Win32.MF_SEPARATOR, 0, null);
        Win32.AppendMenu(menu, Win32.MF_STRING, CmdExit, Loc.Get("Tray_Quit"));

        Win32.GetCursorPos(out var point);
        // Required so the menu closes when the user clicks elsewhere.
        Win32.SetForegroundWindow(_hwnd);
        int command = Win32.TrackPopupMenuEx(menu, Win32.TPM_RIGHTBUTTON | Win32.TPM_RETURNCMD | Win32.TPM_NONOTIFY,
            point.X, point.Y, _hwnd, IntPtr.Zero);
        Win32.PostMessage(_hwnd, Win32.WM_NULL, IntPtr.Zero, IntPtr.Zero);
        Win32.DestroyMenu(menu);

        switch ((uint)command)
        {
            case CmdSettings: OpenSettingsRequested?.Invoke(); break;
            case CmdToggle: ToggleRequested?.Invoke(); break;
            case CmdExit: ExitRequested?.Invoke(); break;
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_TRAYICON:
                switch ((uint)lParam)
                {
                    case Win32.WM_LBUTTONUP: OpenSettingsRequested?.Invoke(); break;
                    case Win32.WM_RBUTTONUP: ShowContextMenu(); break;
                }
                return IntPtr.Zero;

            case Win32.WM_HOTKEY when (int)wParam == HotkeyId:
                ToggleRequested?.Invoke();
                return IntPtr.Zero;

            case WM_SHOW_SETTINGS:
                OpenSettingsRequested?.Invoke();
                return IntPtr.Zero;
        }

        // Explorer restarted: the icon has to be added again.
        if (msg == _taskbarCreatedMessage && _taskbarCreatedMessage != 0)
            AddIcon();

        return Win32.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        var data = CreateIconData(0);
        Win32.Shell_NotifyIcon(Win32.NIM_DELETE, ref data);
        if (HotkeyRegistered)
            Win32.UnregisterHotKey(_hwnd, HotkeyId);
        if (_ownsIcon)
            Win32.DestroyIcon(_icon);
        Win32.DestroyWindow(_hwnd);
    }
}
