using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation;
using Windows.Graphics;
using FocusHaze.Core;
using FocusHaze.Localization;
using FocusHaze.Native;
using WinRT.Interop;

namespace FocusHaze;

/// <summary>
/// The notification-area context menu as a WinUI <see cref="MenuFlyout"/>, so it looks like other Windows 11
/// menus (theme, spacing, rounded corners). A flyout opens from an element in a window, so the menu owns an
/// invisible, click-through window that is moved to the cursor each time.
/// </summary>
internal sealed class TrayMenu
{
    private readonly Window _host;
    private readonly Grid _root = new();
    private readonly MenuFlyout _menu = new() { ShouldConstrainToRootBounds = false };
    private readonly MenuFlyoutItem _settings = new();
    private readonly ToggleMenuFlyoutItem _dimming = new() { KeyboardAcceleratorTextOverride = TrayHost.HotkeyDisplayName };
    private readonly MenuFlyoutItem _quit = new();

    public event Action? SettingsRequested;
    public event Action? ToggleRequested;
    public event Action? ExitRequested;

    public IntPtr Handle { get; }

    public TrayMenu()
    {
        _host = new Window { Content = _root, Title = "FocusHaze" };
        Handle = WindowNative.GetWindowHandle(_host);

        long exStyle = (long)Win32.GetWindowLongPtr(Handle, Win32.GWL_EXSTYLE);
        Win32.SetWindowLongPtr(Handle, Win32.GWL_EXSTYLE,
            (IntPtr)(exStyle | Win32.WS_EX_LAYERED | Win32.WS_EX_TRANSPARENT | Win32.WS_EX_TOOLWINDOW));
        Win32.SetLayeredWindowAttributes(Handle, 0, 0, Win32.LWA_ALPHA);
        _host.AppWindow.IsShownInSwitchers = false;
        if (_host.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        _settings.Click += (_, _) => SettingsRequested?.Invoke();
        _dimming.Click += (_, _) => ToggleRequested?.Invoke();
        _quit.Click += (_, _) => ExitRequested?.Invoke();
        _menu.Items.Add(_settings);
        _menu.Items.Add(_dimming);
        _menu.Items.Add(new MenuFlyoutSeparator());
        _menu.Items.Add(_quit);

        _menu.Closed += (_, _) => _host.AppWindow.Hide();
        // Clicking anywhere else deactivates the host window, which closes the menu.
        _host.Activated += (_, e) =>
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated)
                _menu.Hide();
        };
    }

    public void Show(bool dimmingOn)
    {
        // Texts are set on every opening, so a language switch shows up right away.
        _settings.Text = Loc.Get("Tray_Settings");
        _dimming.Text = Loc.Get("Tray_Dimming");
        _dimming.IsChecked = dimmingOn;
        _quit.Text = Loc.Get("Tray_Quit");

        Win32.GetCursorPos(out var cursor);
        _host.AppWindow.MoveAndResize(new RectInt32(cursor.X, cursor.Y, 1, 1));
        _host.Activate();
        // Foreground is what lets a click elsewhere close the menu; the tray click allows taking it.
        Win32.SetForegroundWindow(Handle);

        if (_root.IsLoaded)
            Open();
        else
            _root.Loaded += OpenOnLoad;
    }

    private void OpenOnLoad(object sender, RoutedEventArgs e)
    {
        _root.Loaded -= OpenOnLoad;
        Open();
    }

    private void Open() =>
        _menu.ShowAt(_root, new FlyoutShowOptions { Position = new Point(0, 0), ShowMode = FlyoutShowMode.Standard });

    public void Close() => _host.Close();
}
