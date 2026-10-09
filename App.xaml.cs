using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using FocusHaze.Core;
using FocusHaze.Localization;
using FocusHaze.Native;

namespace FocusHaze;

public partial class App : Application
{
    private const string MutexName = @"Local\FocusHaze.SingleInstance";

    private static Mutex? s_instanceMutex;

    private DispatcherQueue _dispatcher = null!;
    private DimController? _dimmer;
    private TrayHost? _tray;
    private MainWindow? _window;

    internal static new App Current => (App)Application.Current;

    internal AppSettings Settings { get; private set; } = new();

    internal bool HotkeyAvailable => _tray?.HotkeyRegistered ?? false;

    /// <summary>Raised after settings change from any source (UI, tray menu, hotkey).</summary>
    internal event Action? SettingsChanged;

    public App()
    {
        InitializeComponent();
        // The app lives in the tray; closing the settings window must not end the process.
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        s_instanceMutex = new Mutex(true, MutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            SignalRunningInstance();
            Exit();
            return;
        }

        _dispatcher = DispatcherQueue.GetForCurrentThread();
        Settings = AppSettings.Load();
        StartupManager.MigrateLegacy();
        Loc.SetLanguage(Settings.Language);

        _dimmer = new DimController(Settings);
        _tray = new TrayHost(Settings.Enabled);
        _dimmer.AddIgnoredWindow(_tray.Handle);

        _tray.OpenSettingsRequested += ShowSettings;
        _tray.ToggleRequested += ToggleEnabled;
        // Defer: the request arrives from inside the tray window's own message handler.
        _tray.ExitRequested += () => _dispatcher.TryEnqueue(Shutdown);

        bool startInTray = Environment.GetCommandLineArgs().Contains("--tray", StringComparer.OrdinalIgnoreCase);
        if (!startInTray)
            ShowSettings();
    }

    private static void SignalRunningInstance()
    {
        IntPtr host = Win32.FindWindow(TrayHost.ClassName, null);
        if (host == IntPtr.Zero)
            return;
        Win32.AllowSetForegroundWindow(Win32.ASFW_ANY);
        Win32.PostMessage(host, TrayHost.WM_SHOW_SETTINGS, IntPtr.Zero, IntPtr.Zero);
    }

    internal void ShowSettings()
    {
        if (_window is null)
        {
            // The window is recreated on demand so the app stays light while it sits in the tray.
            _window = new MainWindow();
            _window.Closed += (_, _) => _window = null;
        }
        _window.Activate();
    }

    internal void ToggleEnabled()
    {
        Settings.Enabled = !Settings.Enabled;
        ApplySettings();
    }

    internal void ApplySettings()
    {
        Settings.Save();
        if (Loc.SetLanguage(Settings.Language))
            Localize.Refresh();
        _dimmer?.Apply(Settings);
        _tray?.UpdateState(Settings.Enabled);
        SettingsChanged?.Invoke();
    }

    internal void Shutdown()
    {
        _window?.Close();
        _dimmer?.Dispose();
        _tray?.Dispose();
        s_instanceMutex?.ReleaseMutex();
        Exit();
    }
}
