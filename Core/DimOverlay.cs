using WinDimmer.Native;

namespace WinDimmer.Core;

/// <summary>
/// A click-through, layered Win32 window that paints a solid haze. It is kept in the Z-order
/// directly beneath the active window, so everything behind it appears dimmed.
/// </summary>
internal sealed class DimOverlay : IDisposable
{
    private const string ClassName = "WinDimmer.Overlay";
    private const nuint FadeTimerId = 1;
    private const uint FadeTimerIntervalMs = 10;

    // When focus moves to another window the haze briefly lightens and fades back in,
    // which reads as a soft cross-fade instead of a hard cut.
    private const double SwitchStartFraction = 0.5;

    private readonly Win32.WndProc _wndProc;
    private readonly IntPtr _hwnd;
    private IntPtr _brush;

    private double _alpha;
    private double _fromAlpha;
    private double _toAlpha;
    private long _fadeStart;
    private int _fadeDurationMs;
    private bool _disposed;

    public event Action? DisplayChanged;

    public IntPtr Handle => _hwnd;

    public DimOverlay(uint colorRef)
    {
        _wndProc = WndProc;
        _hwnd = Win32.CreateNativeWindow(ClassName, "WinDimmer Overlay", Win32.WS_POPUP,
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

    /// <summary>Places the haze directly beneath <paramref name="target"/> and fades it to <paramref name="alpha"/>.</summary>
    public void ShowBelow(IntPtr target, Win32.RECT bounds, byte alpha, int fadeMs, bool targetChanged)
    {
        // A non-topmost window cannot sit between topmost windows; for a topmost target,
        // put the haze on top of all regular windows instead.
        bool targetIsTopmost = ((long)Win32.GetWindowLongPtr(target, Win32.GWL_EXSTYLE) & Win32.WS_EX_TOPMOST) != 0;
        IntPtr insertAfter = targetIsTopmost ? Win32.HWND_TOP : target;

        Win32.SetWindowPos(_hwnd, insertAfter, bounds.Left, bounds.Top, bounds.Width, bounds.Height,
            Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW | Win32.SWP_NOOWNERZORDER);

        if (targetChanged && fadeMs > 0)
            ApplyAlpha(Math.Min(_alpha, alpha * SwitchStartFraction));

        AnimateTo(alpha, fadeMs);
    }

    public void Hide(int fadeMs) => AnimateTo(0, fadeMs);

    private void AnimateTo(double target, int durationMs)
    {
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
        if (_alpha <= 0)
            Win32.ShowWindow(_hwnd, Win32.SW_HIDE);
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
