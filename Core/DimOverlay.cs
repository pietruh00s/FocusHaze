using FocusHaze.Native;

namespace FocusHaze.Core;

/// <summary>
/// A click-through, layered Win32 window that paints a solid haze with an animatable opacity.
/// <see cref="DimController"/> uses one to cover the screen beneath the active window and a second,
/// window-sized one to fade in the window that just lost focus.
/// </summary>
internal sealed class DimOverlay : IDisposable
{
    private const nuint FadeTimerId = 1;
    private const uint FadeTimerIntervalMs = 10;

    private readonly Win32.WndProc _wndProc;
    private readonly IntPtr _hwnd;
    private IntPtr _brush;

    private double _alpha;
    private double _fromAlpha;
    private double _toAlpha;
    private long _fadeStart;
    private int _fadeDurationMs;
    private Action? _fadeCompleted;
    private bool _disposed;

    public event Action? DisplayChanged;

    public IntPtr Handle => _hwnd;

    /// <summary>True while the window is shown (including while it fades out).</summary>
    public bool IsShown { get; private set; }

    public Win32.RECT Bounds { get; private set; }

    /// <param name="className">Unique per instance: the window class carries this instance's WndProc.</param>
    public DimOverlay(string className, uint colorRef)
    {
        _wndProc = WndProc;
        _hwnd = Win32.CreateNativeWindow(className, "FocusHaze Overlay", Win32.WS_POPUP,
            Win32.WS_EX_LAYERED | Win32.WS_EX_TRANSPARENT | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE,
            _wndProc);
        SetColor(colorRef);
        ApplyAlpha(0);
    }

    public void SetColor(uint colorRef)
    {
        IntPtr old = _brush;
        _brush = Win32.CreateSolidBrush(colorRef);
        if (old != IntPtr.Zero)
            Win32.DeleteObject(old);
        Win32.InvalidateRect(_hwnd, IntPtr.Zero, true);
    }

    /// <summary>Shows the haze over <paramref name="bounds"/>, directly beneath <paramref name="insertAfter"/>.</summary>
    public void Place(IntPtr insertAfter, Win32.RECT bounds)
    {
        Win32.SetWindowPos(_hwnd, insertAfter, bounds.Left, bounds.Top, bounds.Width, bounds.Height,
            Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW | Win32.SWP_NOOWNERZORDER);
        Bounds = bounds;
        IsShown = true;
    }

    /// <summary>Queues re-placing the haze (at its current bounds) beneath <paramref name="insertAfter"/>.</summary>
    public IntPtr DeferPlace(IntPtr batch, IntPtr insertAfter)
    {
        IsShown = true;
        return Win32.DeferWindowPos(batch, _hwnd, insertAfter, Bounds.Left, Bounds.Top, Bounds.Width, Bounds.Height,
            Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW | Win32.SWP_NOOWNERZORDER);
    }

    /// <summary>Queues hiding the window; call <see cref="MarkHidden"/> once the batch has been applied.</summary>
    public IntPtr DeferHide(IntPtr batch) =>
        Win32.DeferWindowPos(batch, _hwnd, IntPtr.Zero, 0, 0, 0, 0,
            Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE | Win32.SWP_HIDEWINDOW);

    public void HideNow()
    {
        Win32.ShowWindow(_hwnd, Win32.SW_HIDE);
        MarkHidden();
    }

    /// <summary>Resets state after the window was hidden externally. Opacity is cleared only now,
    /// so the haze never visibly disappears before the window itself does.</summary>
    public void MarkHidden()
    {
        Win32.KillTimer(_hwnd, FadeTimerId);
        _fadeCompleted = null;
        IsShown = false;
        ApplyAlpha(0);
    }

    public void SetAlpha(double alpha)
    {
        Win32.KillTimer(_hwnd, FadeTimerId);
        _fadeCompleted = null;
        ApplyAlpha(alpha);
    }

    /// <summary>Fades out and hides the window.</summary>
    public void Hide(int fadeMs) => FadeTo(0, fadeMs);

    public void FadeTo(double target, int durationMs, Action? completed = null)
    {
        _fadeCompleted = completed;
        if (durationMs <= 0 || Math.Abs(target - _alpha) < 1)
        {
            Win32.KillTimer(_hwnd, FadeTimerId);
            ApplyAlpha(target);
            OnFadeFinished();
            return;
        }

        _fromAlpha = _alpha;
        _toAlpha = target;
        _fadeDurationMs = durationMs;
        _fadeStart = Environment.TickCount64;
        Win32.SetTimer(_hwnd, FadeTimerId, FadeTimerIntervalMs, IntPtr.Zero);
    }

    private void OnFadeTick()
    {
        double t = Math.Clamp((Environment.TickCount64 - _fadeStart) / (double)_fadeDurationMs, 0, 1);
        double eased = 1 - Math.Pow(1 - t, 3); // ease-out cubic
        ApplyAlpha(_fromAlpha + (_toAlpha - _fromAlpha) * eased);

        if (t >= 1)
        {
            Win32.KillTimer(_hwnd, FadeTimerId);
            OnFadeFinished();
        }
    }

    private void OnFadeFinished()
    {
        if (_alpha <= 0 && IsShown)
        {
            Win32.ShowWindow(_hwnd, Win32.SW_HIDE);
            IsShown = false;
        }

        Action? completed = _fadeCompleted;
        _fadeCompleted = null;
        completed?.Invoke();
    }

    private void ApplyAlpha(double alpha)
    {
        _alpha = Math.Clamp(alpha, 0, 255);
        Win32.SetLayeredWindowAttributes(_hwnd, 0, (byte)Math.Round(_alpha), Win32.LWA_ALPHA);
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case Win32.WM_ERASEBKGND:
                Win32.GetClientRect(hWnd, out var rect);
                Win32.FillRect(wParam, ref rect, _brush);
                return 1;

            case Win32.WM_TIMER when (nuint)wParam == FadeTimerId:
                OnFadeTick();
                return IntPtr.Zero;

            case Win32.WM_DISPLAYCHANGE:
                DisplayChanged?.Invoke();
                break;
        }
        return Win32.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Win32.KillTimer(_hwnd, FadeTimerId);
        Win32.DestroyWindow(_hwnd);
        if (_brush != IntPtr.Zero)
            Win32.DeleteObject(_brush);
    }
}
