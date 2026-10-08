using WinDimmer.Native;

namespace WinDimmer.Core;

/// <summary>
/// Watches foreground/minimize/destroy events system-wide and keeps the haze positioned under the active window.
/// </summary>
internal sealed class DimController : IDisposable
{
    // Shell surfaces that may briefly take focus; the haze stays where it is while they are active.
    private static readonly HashSet<string> IgnoredClasses = new(StringComparer.Ordinal)
    {
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "NotifyIconOverflowWindow",
        "TopLevelWindowForOverflowXamlIsland",
        "XamlExplorerHostIslandWindow",
        "MultitaskingViewFrame",
        "ForegroundStaging",
        "TaskListThumbnailWnd",
        "Windows.UI.Core.CoreWindow",
        "#32768", // popup menu
    };

    // Focusing the desktop (e.g. Win+D) lifts the haze entirely.
    private static readonly HashSet<string> DesktopClasses = new(StringComparer.Ordinal) { "Progman", "WorkerW" };

    private enum TargetKind { Dim, Hide, Ignore }

    private readonly DimOverlay _overlay;
    private readonly Win32.WinEventProc _winEventProc;
    private readonly List<IntPtr> _hooks = [];
    private readonly HashSet<IntPtr> _ownWindows = [];
    private AppSettings _settings;
    private IntPtr _target;

    public DimController(AppSettings settings)
    {
        _settings = settings;
        _overlay = new DimOverlay(settings.ColorRef);
        _overlay.DisplayChanged += () => Refresh();
        _ownWindows.Add(_overlay.Handle);

        _winEventProc = OnWinEvent;
        Hook(Win32.EVENT_SYSTEM_FOREGROUND);
        Hook(Win32.EVENT_SYSTEM_MOVESIZEEND);
        Hook(Win32.EVENT_SYSTEM_MINIMIZESTART, Win32.EVENT_SYSTEM_MINIMIZEEND);
        Hook(Win32.EVENT_OBJECT_DESTROY);
        Hook(Win32.EVENT_OBJECT_HIDE);
        Hook(Win32.EVENT_OBJECT_CLOAKED);

        Refresh();
    }

    /// <summary>Windows of this app that should never become the haze target (tray host, menus).</summary>
    public void AddIgnoredWindow(IntPtr hwnd) => _ownWindows.Add(hwnd);

    public void Apply(AppSettings settings)
    {
        _settings = settings;
        _overlay.SetColor(settings.ColorRef);
        Refresh();
    }

    private void Hook(uint eventMin, uint? eventMax = null)
    {
        IntPtr hook = Win32.SetWinEventHook(eventMin, eventMax ?? eventMin, IntPtr.Zero, _winEventProc,
            0, 0, Win32.WINEVENT_OUTOFCONTEXT);
        if (hook != IntPtr.Zero)
            _hooks.Add(hook);
    }

    private void OnWinEvent(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject != Win32.OBJID_WINDOW || idChild != 0)
            return;

        switch (eventType)
        {
            case Win32.EVENT_SYSTEM_FOREGROUND:
            case Win32.EVENT_SYSTEM_MINIMIZEEND:
                Refresh(switching: true);
                break;

            case Win32.EVENT_SYSTEM_MINIMIZESTART when hwnd == _target:
                _target = IntPtr.Zero;
                _overlay.Hide(FadeMs);
                break;

            case Win32.EVENT_SYSTEM_MOVESIZEEND when hwnd == _target && _settings.ActiveDisplayOnly:
                Refresh();
                break;

            case Win32.EVENT_OBJECT_DESTROY when hwnd == _target:
            case Win32.EVENT_OBJECT_HIDE when hwnd == _target:
            case Win32.EVENT_OBJECT_CLOAKED when hwnd == _target:
                Refresh(switching: true);
                break;
        }
    }

    private int FadeMs => _settings.Animate ? _settings.FadeDurationMs : 0;

    public void Refresh(bool switching = false)
    {
        if (!_settings.Enabled)
        {
            _target = IntPtr.Zero;
            _overlay.Hide(FadeMs);
            return;
        }

        IntPtr foreground = Win32.GetForegroundWindow();
        switch (Classify(foreground))
        {
            case TargetKind.Ignore:
                return;
            case TargetKind.Hide:
                _target = IntPtr.Zero;
                _overlay.Hide(FadeMs);
                return;
        }

        bool changed = foreground != _target;
        _target = foreground;
        _overlay.ShowBelow(foreground, GetBounds(foreground), _settings.Alpha, FadeMs, changed && switching);
    }

    private TargetKind Classify(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !Win32.IsWindow(hwnd))
            return TargetKind.Hide;
        if (_ownWindows.Contains(hwnd))
            return TargetKind.Ignore;

        string className = Win32.GetClassName(hwnd);
        if (DesktopClasses.Contains(className))
            return TargetKind.Hide;
        if (IgnoredClasses.Contains(className))
            return TargetKind.Ignore;
        if (Win32.IsIconic(hwnd) || !Win32.IsWindowVisible(hwnd) || Win32.IsCloaked(hwnd))
            return TargetKind.Hide;

        return TargetKind.Dim;
    }

    private Win32.RECT GetBounds(IntPtr hwnd)
    {
        if (_settings.ActiveDisplayOnly)
        {
            var info = new Win32.MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32.MONITORINFO>() };
            IntPtr monitor = Win32.MonitorFromWindow(hwnd, Win32.MONITOR_DEFAULTTONEAREST);
            if (Win32.GetMonitorInfo(monitor, ref info))
                return info.rcMonitor;
        }

        int x = Win32.GetSystemMetrics(Win32.SM_XVIRTUALSCREEN);
        int y = Win32.GetSystemMetrics(Win32.SM_YVIRTUALSCREEN);
        return new Win32.RECT(x, y,
            x + Win32.GetSystemMetrics(Win32.SM_CXVIRTUALSCREEN),
            y + Win32.GetSystemMetrics(Win32.SM_CYVIRTUALSCREEN));
    }

    public void Dispose()
    {
        foreach (IntPtr hook in _hooks)
            Win32.UnhookWinEvent(hook);
        _hooks.Clear();
        _overlay.Dispose();
    }
}
