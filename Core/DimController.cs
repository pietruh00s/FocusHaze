using FocusHaze.Native;
using Microsoft.UI.Dispatching;

namespace FocusHaze.Core;

/// <summary>
/// Watches foreground/minimize/destroy events system-wide and keeps the haze positioned under the active window
/// (with <see cref="DisplayMode.Each"/>, under the most recently used window of every display).
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

    // One zone for the whole desktop or the active display, or one per display. Created on demand and
    // kept (hidden) when unused, since their window classes can only be registered once.
    private readonly List<DimZone> _zones = [];

    // Topmost haze cut to the taskbars of dimmed displays (AppSettings.DimTaskbar); spans the virtual screen.
    private readonly DimOverlay _taskbar;

    private readonly Win32.WinEventProc _winEventProc;
    private readonly List<IntPtr> _hooks = [];
    private readonly HashSet<IntPtr> _ownWindows = [];
    private readonly DispatcherQueue _dispatcher;
    private AppSettings _settings;

    // The foreground window the zones were last arranged for.
    private IntPtr _target;

    // Holds back dimming after a window switch for AppSettings.DimDelayMs.
    private readonly DispatcherQueueTimer _delay;
    private IntPtr _delayFor;

    public DimController(AppSettings settings)
    {
        _settings = settings;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        AddZone().DisplayChanged += () => Refresh();
        _taskbar = new DimOverlay("FocusHaze.TaskbarOverlay", settings.ColorRef);
        _ownWindows.Add(_taskbar.Handle);

        _delay = _dispatcher.CreateTimer();
        _delay.IsRepeating = false;
        _delay.Tick += (_, _) => SafeRefresh(switching: true, delayed: true);

        _winEventProc = OnWinEvent;
        Hook(Win32.EVENT_SYSTEM_FOREGROUND);
        Hook(Win32.EVENT_SYSTEM_MOVESIZEEND);
        Hook(Win32.EVENT_SYSTEM_MINIMIZESTART, Win32.EVENT_SYSTEM_MINIMIZEEND);
        Hook(Win32.EVENT_OBJECT_DESTROY, Win32.EVENT_OBJECT_HIDE);
        Hook(Win32.EVENT_OBJECT_CLOAKED, Win32.EVENT_OBJECT_UNCLOAKED);

        Refresh();
    }

    /// <summary>Windows of this app that should never become the haze target (tray host, menus).</summary>
    public void AddIgnoredWindow(IntPtr hwnd) => _ownWindows.Add(hwnd);

    public void Apply(AppSettings settings)
    {
        _settings = settings;
        foreach (DimZone zone in _zones)
            zone.SetColor(settings.ColorRef);
        _taskbar.SetColor(settings.ColorRef);
        Refresh();
    }

    private DimZone AddZone()
    {
        var zone = new DimZone(_zones.Count, _settings.ColorRef);
        _zones.Add(zone);
        _ownWindows.UnionWith(zone.Handles);
        return zone;
    }

    private void Hook(uint eventMin, uint? eventMax = null)
    {
        IntPtr hook = Win32.SetWinEventHook(eventMin, eventMax ?? eventMin, IntPtr.Zero, _winEventProc,
            0, 0, Win32.WINEVENT_OUTOFCONTEXT);
        if (hook != IntPtr.Zero)
            _hooks.Add(hook);
    }

    // Called from native code: an exception escaping here would terminate the process.
    private void OnWinEvent(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        try
        {
            OnWinEventCore(eventType, hwnd, idObject, idChild);
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "win event 0x" + eventType.ToString("X4"));
        }
    }

    private void OnWinEventCore(uint eventType, IntPtr hwnd, int idObject, int idChild)
    {
        if (idObject != Win32.OBJID_WINDOW || idChild != 0)
            return;

        // The window being faded in went away: finish its transition right away.
        if (eventType is Win32.EVENT_SYSTEM_MINIMIZESTART or Win32.EVENT_OBJECT_DESTROY
                or Win32.EVENT_OBJECT_HIDE or Win32.EVENT_OBJECT_CLOAKED)
        {
            foreach (DimZone zone in _zones)
                if (zone.TransitionFrom == hwnd)
                    zone.CompleteTransition();
        }

        switch (eventType)
        {
            case Win32.EVENT_SYSTEM_FOREGROUND:
            case Win32.EVENT_SYSTEM_MINIMIZEEND:
                Refresh(switching: true);
                break;

            case Win32.EVENT_SYSTEM_MINIMIZESTART when hwnd == _target:
                HideAll();
                break;

            // The bright window of another display was minimized: once it is, the next one there takes its place.
            case Win32.EVENT_SYSTEM_MINIMIZESTART when IsZoneTarget(hwnd):
                _dispatcher.TryEnqueue(() => SafeRefresh());
                break;

            case Win32.EVENT_SYSTEM_MOVESIZEEND when IsZoneTarget(hwnd) && _settings.Displays != DisplayMode.All:
                Refresh();
                break;

            // Explorer and packaged apps take the foreground while still hidden or cloaked, which lifted the
            // haze; once the foreground window actually appears, dim around it.
            case Win32.EVENT_OBJECT_SHOW or Win32.EVENT_OBJECT_UNCLOAKED
                when hwnd != _target && hwnd == Win32.GetForegroundWindow():
                Refresh(switching: true);
                break;

            case Win32.EVENT_OBJECT_DESTROY when IsZoneTarget(hwnd):
            case Win32.EVENT_OBJECT_HIDE when IsZoneTarget(hwnd):
            case Win32.EVENT_OBJECT_CLOAKED when IsZoneTarget(hwnd):
                Refresh(switching: true);
                break;
        }
    }

    private bool IsZoneTarget(IntPtr hwnd) => hwnd != IntPtr.Zero && _zones.Exists(zone => zone.Target == hwnd);

    private int FadeMs => _settings.Animate ? _settings.FadeDurationMs : 0;

    // For callbacks from the dispatcher, where an exception would take the app down.
    private void SafeRefresh(bool switching = false, bool delayed = false)
    {
        try
        {
            Refresh(switching, delayed);
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "refresh");
        }
    }

    public void Refresh(bool switching = false, bool delayed = false)
    {
        if (!_settings.Enabled)
        {
            HideAll();
            return;
        }

        IntPtr foreground = Win32.GetForegroundWindow();
        switch (Classify(foreground))
        {
            case TargetKind.Ignore:
                // Using the taskbar, Start or the tray: show the taskbar undimmed meanwhile.
                if (IsShellSurface(foreground))
                    _taskbar.Hide(FadeMs);
                return;
            case TargetKind.Hide:
                HideAll();
                return;
        }

        // The newly activated window is already above the haze, so until the delay runs out the window
        // that lost focus simply stays bright as well.
        if (switching && !delayed && _settings.DimDelayMs > 0 && foreground != _target)
        {
            // Repeated events for the same window must not restart the countdown.
            if (_delay.IsRunning && _delayFor == foreground)
                return;
            _delayFor = foreground;
            _delay.Interval = TimeSpan.FromMilliseconds(_settings.DimDelayMs);
            _delay.Start();
            return;
        }
        _delay.Stop();

        _target = foreground;
        List<(IntPtr Target, Win32.RECT Bounds)> plan = Plan(foreground);
        while (_zones.Count < plan.Count)
            AddZone();

        byte alpha = _settings.Alpha;
        int fadeMs = FadeMs;
        for (int i = 0; i < _zones.Count; i++)
        {
            if (i < plan.Count && plan[i].Target != IntPtr.Zero)
                _zones[i].Show(plan[i].Target, plan[i].Bounds, switching, alpha, fadeMs);
            else
                _zones[i].Hide(fadeMs);
        }

        ShowTaskbarHaze(plan.Where(p => p.Target != IntPtr.Zero).Select(p => p.Bounds), alpha, fadeMs);
    }

    /// <summary>Which window stays bright over which area, one entry per zone.</summary>
    private List<(IntPtr Target, Win32.RECT Bounds)> Plan(IntPtr foreground)
    {
        switch (_settings.Displays)
        {
            case DisplayMode.Active:
                IntPtr monitor = Win32.MonitorFromWindow(foreground, Win32.MONITOR_DEFAULTTONEAREST);
                return [(foreground, MonitorBounds(monitor) ?? VirtualScreen())];

            case DisplayMode.Each:
                IntPtr active = Win32.MonitorFromWindow(foreground, Win32.MONITOR_DEFAULTTONEAREST);
                return Monitors()
                    .Select(m => (m.Handle == active ? foreground : TopWindowOn(m.Handle), m.Bounds))
                    .ToList();

            default:
                return [(foreground, VirtualScreen())];
        }
    }

    // The highest app window on a display, which is the one used there most recently (activating a window
    // brings it to the top). Topmost windows are skipped: they are never dimmed anyway.
    private IntPtr TopWindowOn(IntPtr monitor)
    {
        for (IntPtr h = Win32.GetTopWindow(IntPtr.Zero); h != IntPtr.Zero; h = Win32.GetWindow(h, Win32.GW_HWNDNEXT))
        {
            if (Win32.IsTopmost(h) || Win32.IsToolWindow(h) || Win32.GetWindow(h, Win32.GW_OWNER) != IntPtr.Zero
                || Classify(h) != TargetKind.Dim || !Win32.TryGetFrameBounds(h, out var frame) || frame.IsEmpty)
                continue;
            if (Win32.MonitorFromWindow(h, Win32.MONITOR_DEFAULTTONEAREST) == monitor)
                return h;
        }
        return IntPtr.Zero;
    }

    private static List<(IntPtr Handle, Win32.RECT Bounds)> Monitors()
    {
        var monitors = new List<(IntPtr, Win32.RECT)>();
        Win32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr hdc, ref Win32.RECT rect, IntPtr data) =>
        {
            monitors.Add((monitor, rect));
            return true;
        }, IntPtr.Zero);
        return monitors;
    }

    private static Win32.RECT? MonitorBounds(IntPtr monitor)
    {
        var info = new Win32.MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32.MONITORINFO>() };
        return Win32.GetMonitorInfo(monitor, ref info) ? info.rcMonitor : null;
    }

    private void ShowTaskbarHaze(IEnumerable<Win32.RECT> dimmed, byte alpha, int fadeMs)
    {
        var rects = _settings.DimTaskbar
            ? TaskbarRects().SelectMany(t => dimmed.Select(d => t.Intersect(d))).Where(r => !r.IsEmpty).ToList()
            : [];
        if (rects.Count == 0)
        {
            _taskbar.Hide(fadeMs);
            return;
        }

        // Hidden means fully transparent, so nothing shows before the region is set.
        if (!_taskbar.IsShown)
            _taskbar.SetAlpha(0);
        // Placing it again each time puts it back above the taskbar, which raises itself when used.
        _taskbar.Place(Win32.HWND_TOPMOST, VirtualScreen());
        _taskbar.SetRegion(rects);
        _taskbar.FadeTo(alpha, fadeMs);
    }

    // shortcut: an auto-hiding taskbar is dimmed where it was at the last refresh, not as it slides in;
    // tracking EVENT_OBJECT_LOCATIONCHANGE for the taskbar windows would fix that if people ask.
    private static IEnumerable<Win32.RECT> TaskbarRects()
    {
        foreach (string className in (string[])["Shell_TrayWnd", "Shell_SecondaryTrayWnd"])
        {
            for (IntPtr h = Win32.FindWindowEx(IntPtr.Zero, IntPtr.Zero, className, null); h != IntPtr.Zero;
                 h = Win32.FindWindowEx(IntPtr.Zero, h, className, null))
            {
                if (Win32.IsWindowVisible(h) && Win32.GetWindowRect(h, out var rect))
                    yield return rect;
            }
        }
    }

    private void HideAll()
    {
        _delay.Stop();
        _target = IntPtr.Zero;
        foreach (DimZone zone in _zones)
            zone.Hide(FadeMs);
        _taskbar.Hide(FadeMs);
    }

    // Popup menus are ignored too, but they belong to apps rather than to the taskbar.
    private static bool IsShellSurface(IntPtr hwnd)
    {
        string className = Win32.GetClassName(hwnd);
        return className != "#32768" && IgnoredClasses.Contains(className);
    }

    private TargetKind Classify(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !Win32.IsWindow(hwnd))
            return TargetKind.Hide;
        // Also the tray menu's popup, which is owned by the menu's host window.
        if (_ownWindows.Contains(hwnd) || _ownWindows.Contains(Win32.GetAncestor(hwnd, Win32.GA_ROOTOWNER)))
            return TargetKind.Ignore;

        string className = Win32.GetClassName(hwnd);
        if (DesktopClasses.Contains(className))
            return TargetKind.Hide;
        if (IgnoredClasses.Contains(className))
            return TargetKind.Ignore;
        if (Win32.IsIconic(hwnd))
            return TargetKind.Hide;
        // Still appearing (EVENT_OBJECT_SHOW/UNCLOAKED follows), or hidden for good: keep the haze where it is.
        if (!Win32.IsWindowVisible(hwnd) || Win32.IsCloaked(hwnd))
            return TargetKind.Ignore;

        return TargetKind.Dim;
    }

    private static Win32.RECT VirtualScreen()
    {
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
        _delay.Stop();
        foreach (DimZone zone in _zones)
            zone.Dispose();
        _taskbar.Dispose();
    }
}
