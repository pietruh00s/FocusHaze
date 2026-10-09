using FocusHaze.Native;

namespace FocusHaze.Core;

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

    // Full-screen haze kept directly beneath the active window.
    private readonly DimOverlay _haze;

    // Window-sized haze that fades in over the window that just lost focus, while _haze stays
    // beneath it; once faded in, _haze moves beneath the new active window in a single step.
    private readonly DimOverlay _transition;
    private IntPtr _transitionFrom;

    private readonly Win32.WinEventProc _winEventProc;
    private readonly List<IntPtr> _hooks = [];
    private readonly HashSet<IntPtr> _ownWindows = [];
    private AppSettings _settings;
    private IntPtr _target;

    public DimController(AppSettings settings)
    {
        _settings = settings;
        _haze = new DimOverlay("FocusHaze.Overlay", settings.ColorRef);
        _haze.DisplayChanged += () => Refresh();
        _transition = new DimOverlay("FocusHaze.TransitionOverlay", settings.ColorRef);
        _ownWindows.Add(_haze.Handle);
        _ownWindows.Add(_transition.Handle);

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
        _haze.SetColor(settings.ColorRef);
        _transition.SetColor(settings.ColorRef);
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

        // The window being faded in went away: finish its transition right away.
        if (hwnd == _transitionFrom && eventType is Win32.EVENT_SYSTEM_MINIMIZESTART
                or Win32.EVENT_OBJECT_DESTROY or Win32.EVENT_OBJECT_HIDE or Win32.EVENT_OBJECT_CLOAKED)
            CompleteTransition();

        switch (eventType)
        {
            case Win32.EVENT_SYSTEM_FOREGROUND:
            case Win32.EVENT_SYSTEM_MINIMIZEEND:
                Refresh(switching: true);
                break;

            case Win32.EVENT_SYSTEM_MINIMIZESTART when hwnd == _target:
                HideAll();
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
            HideAll();
            return;
        }

        IntPtr foreground = Win32.GetForegroundWindow();
        switch (Classify(foreground))
        {
            case TargetKind.Ignore:
                return;
            case TargetKind.Hide:
                HideAll();
                return;
        }

        // A switch that arrives mid-transition snaps the previous one to its end state first.
        CompleteTransition();

        IntPtr previous = _target;
        _target = foreground;
        Win32.RECT bounds = GetBounds(foreground);
        byte alpha = _settings.Alpha;
        int fadeMs = FadeMs;

        // Switching windows within an already dimmed area: only the window that lost focus fades in.
        if (switching && fadeMs > 0 && previous != foreground && _haze.IsShown && _haze.Bounds.Equals(bounds)
            && TryStartTransition(previous, foreground, bounds, alpha, fadeMs))
            return;

        // The area was clear until now (haze hidden, or moved to another display): fade it in from zero.
        bool newArea = !_haze.IsShown || (switching && !_haze.Bounds.Equals(bounds));
        if (newArea && fadeMs > 0)
            _haze.SetAlpha(0);

        _haze.Place(InsertBelow(foreground), bounds);
        _haze.FadeTo(alpha, fadeMs);
    }

    private bool TryStartTransition(IntPtr previous, IntPtr foreground, Win32.RECT bounds, byte alpha, int fadeMs)
    {
        if (previous == IntPtr.Zero || !Win32.IsWindow(previous) || Win32.IsIconic(previous)
            || !Win32.IsWindowVisible(previous) || Win32.IsCloaked(previous) || Win32.IsTopmost(previous)
            || !Win32.TryGetFrameBounds(previous, out var frame))
            return false;

        Win32.RECT area = frame.Intersect(bounds);
        if (area.IsEmpty)
            return false;

        // _haze still sits beneath the previous window, so the rest of the screen stays dimmed throughout.
        _haze.FadeTo(alpha, fadeMs);

        _transitionFrom = previous;
        _transition.SetAlpha(0);
        _transition.Place(InsertBelow(foreground), area);
        _transition.FadeTo(alpha, fadeMs, CompleteTransition);
        return true;
    }

    /// <summary>Moves the haze beneath the active window and drops the transition overlay in one step.</summary>
    private void CompleteTransition()
    {
        if (_transitionFrom == IntPtr.Zero)
            return;
        _transitionFrom = IntPtr.Zero;

        IntPtr batch = Win32.BeginDeferWindowPos(2);
        if (_target != IntPtr.Zero && Win32.IsWindow(_target))
            batch = _haze.DeferPlace(batch, InsertBelow(_target));
        batch = _transition.DeferHide(batch);
        Win32.EndDeferWindowPos(batch);
        _transition.MarkHidden();
    }

    private void HideAll()
    {
        _target = IntPtr.Zero;
        if (_transitionFrom != IntPtr.Zero)
        {
            _transitionFrom = IntPtr.Zero;
            _transition.HideNow();
        }
        _haze.Hide(FadeMs);
    }

    // A regular window can't sit between topmost ones; for a topmost target, go above all regular windows.
    private static IntPtr InsertBelow(IntPtr target) => Win32.IsTopmost(target) ? Win32.HWND_TOP : target;

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
        _transition.Dispose();
        _haze.Dispose();
    }
}
