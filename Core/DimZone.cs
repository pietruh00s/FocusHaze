using FocusHaze.Native;

namespace FocusHaze.Core;

/// <summary>
/// The haze over one area (the whole desktop or a single display), kept directly beneath the window
/// that stays bright there, with the fade for the window that just lost that place.
/// </summary>
internal sealed class DimZone : IDisposable
{
    // Area-sized haze kept directly beneath the target window.
    private readonly DimOverlay _haze;

    // Window-sized haze that fades in over the window that just lost focus, while _haze stays
    // beneath it; once faded in, _haze moves beneath the new target in a single step.
    private readonly DimOverlay _transition;

    /// <param name="index">Zones are never destroyed, so each index registers its own window classes once.</param>
    public DimZone(int index, uint colorRef)
    {
        string suffix = index == 0 ? "" : "." + index;
        _haze = new DimOverlay("FocusHaze.Overlay" + suffix, colorRef);
        _transition = new DimOverlay("FocusHaze.TransitionOverlay" + suffix, colorRef);
    }

    public event Action? DisplayChanged
    {
        add => _haze.DisplayChanged += value;
        remove => _haze.DisplayChanged -= value;
    }

    public IntPtr[] Handles => [_haze.Handle, _transition.Handle];

    /// <summary>The window kept bright above the haze; zero while hidden.</summary>
    public IntPtr Target { get; private set; }

    /// <summary>The window currently fading into the haze, if any.</summary>
    public IntPtr TransitionFrom { get; private set; }

    public void SetColor(uint colorRef)
    {
        _haze.SetColor(colorRef);
        _transition.SetColor(colorRef);
    }

    public void Show(IntPtr target, Win32.RECT bounds, bool switching, byte alpha, int fadeMs)
    {
        // Another event for a switch that is already fading: let it finish.
        if (switching && target == Target && TransitionFrom != IntPtr.Zero)
            return;

        // A switch that arrives mid-transition snaps the previous one to its end state first.
        CompleteTransition();

        IntPtr previous = Target;
        Target = target;

        // Switching windows within an already dimmed area: only the window that lost focus fades in.
        if (switching && fadeMs > 0 && previous != target && _haze.IsShown && _haze.Bounds.Equals(bounds)
            && TryStartTransition(previous, target, bounds, alpha, fadeMs))
            return;

        // The area was clear until now (haze hidden, or moved to another display): fade it in from zero.
        bool newArea = !_haze.IsShown || (switching && !_haze.Bounds.Equals(bounds));
        if (newArea && fadeMs > 0)
            _haze.SetAlpha(0);

        _haze.Place(InsertBelow(target), bounds);
        _haze.FadeTo(alpha, fadeMs);
    }

    private bool TryStartTransition(IntPtr previous, IntPtr target, Win32.RECT bounds, byte alpha, int fadeMs)
    {
        if (previous == IntPtr.Zero || !Win32.IsWindow(previous) || Win32.IsIconic(previous)
            || !Win32.IsWindowVisible(previous) || Win32.IsCloaked(previous) || Win32.IsTopmost(previous)
            || !Win32.TryGetFrameBounds(previous, out var frame))
            return false;

        Win32.RECT area = frame.Intersect(bounds);
        if (area.IsEmpty)
            return false;

        // _haze still sits beneath the previous window, so the rest of the area stays dimmed throughout.
        _haze.FadeTo(alpha, fadeMs);

        TransitionFrom = previous;
        _transition.SetAlpha(0);
        _transition.Place(InsertBelow(target), area);
        _transition.FadeTo(alpha, fadeMs, CompleteTransition);
        return true;
    }

    /// <summary>Moves the haze beneath the target and drops the transition overlay in one step.</summary>
    public void CompleteTransition()
    {
        if (TransitionFrom == IntPtr.Zero)
            return;
        TransitionFrom = IntPtr.Zero;

        IntPtr batch = Win32.BeginDeferWindowPos(2);
        if (Target != IntPtr.Zero && Win32.IsWindow(Target))
            batch = _haze.DeferPlace(batch, InsertBelow(Target));
        batch = _transition.DeferHide(batch);
        Win32.EndDeferWindowPos(batch);
        _transition.MarkHidden();
    }

    public void Hide(int fadeMs)
    {
        Target = IntPtr.Zero;
        if (TransitionFrom != IntPtr.Zero)
        {
            TransitionFrom = IntPtr.Zero;
            _transition.HideNow();
        }
        _haze.Hide(fadeMs);
    }

    // A regular window can't sit between topmost ones; for a topmost target, go above all regular windows.
    private static IntPtr InsertBelow(IntPtr target) => Win32.IsTopmost(target) ? Win32.HWND_TOP : target;

    public void Dispose()
    {
        _transition.Dispose();
        _haze.Dispose();
    }
}
