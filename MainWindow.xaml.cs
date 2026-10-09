using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using FocusHaze.Core;
using FocusHaze.Localization;
using FocusHaze.Native;
using WinRT.Interop;
using Color = Windows.UI.Color;

namespace FocusHaze;

public sealed partial class MainWindow : Window
{
    private static readonly string[] PresetColors = ["#000000", "#1B1F3B", "#2B1B10", "#0F2A1E", "#3A3A3A"];

    // Guards against control events echoing back into settings while the UI is being synced.
    // Starts true: InitializeComponent coerces slider values to their Minimum and raises
    // ValueChanged, which must not overwrite the saved settings.
    private bool _syncing = true;

    private static AppSettings Settings => App.Current.Settings;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "FocusHaze.ico"));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.IsMaximizable = false;
        SizeAndCenter(620, 760);

        BuildColorPresets();
        BuildLanguageList();
        SyncFromSettings();

        App.Current.SettingsChanged += SyncFromSettings;
        Closed += (_, _) => App.Current.SettingsChanged -= SyncFromSettings;
    }

    private void SizeAndCenter(int width, int height)
    {
        double scale = Win32.GetDpiForWindow(WindowNative.GetWindowHandle(this)) / 96.0;
        var size = new SizeInt32((int)(width * scale), (int)(height * scale));
        RectInt32 workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        size.Height = Math.Min(size.Height, workArea.Height);
        AppWindow.MoveAndResize(new RectInt32(
            workArea.X + (workArea.Width - size.Width) / 2,
            workArea.Y + (workArea.Height - size.Height) / 2,
            size.Width, size.Height));
    }

    private void BuildColorPresets()
    {
        foreach (string hex in PresetColors)
        {
            var button = new Button
            {
                Width = 32,
                Height = 32,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(ToColor(hex)),
                Tag = hex,
            };
            ToolTipService.SetToolTip(button, hex);
            button.Click += (_, _) => HazeColorPicker.Color = ToColor(hex);
            PresetPanel.Children.Add(button);
        }
    }

    private void BuildLanguageList()
    {
        var systemItem = new ComboBoxItem { Tag = "" };
        Localize.SetKey(systemItem, "Language_System");
        LanguageCombo.Items.Add(systemItem);

        foreach (string language in Loc.SupportedLanguages)
            LanguageCombo.Items.Add(new ComboBoxItem { Content = Loc.NativeName(language), Tag = language });
    }

    private void SyncFromSettings()
    {
        _syncing = true;
        try
        {
            LanguageCombo.SelectedItem = LanguageCombo.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => (string)item.Tag == Loc.Language) ?? LanguageCombo.Items[0];
            AutomationProperties.SetName(ColorButton, Loc.Get("Color_Title"));
            AutomationProperties.SetName(LanguageCombo, Loc.Get("Language_Title"));

            EnabledToggle.IsOn = Settings.Enabled;
            IntensitySlider.Value = Settings.Intensity;
            IntensityText.Text = $"{Settings.Intensity}%";
            HazeColorPicker.Color = ToColor(Settings.Color);
            ColorSwatch.Background = new SolidColorBrush(ToColor(Settings.Color));
            AnimateToggle.IsOn = Settings.Animate;
            FadeSlider.Value = Settings.FadeDurationMs;
            FadeText.Text = $"{Settings.FadeDurationMs} ms";
            FadePanel.Visibility = Settings.Animate ? Visibility.Visible : Visibility.Collapsed;
            ActiveDisplayToggle.IsOn = Settings.ActiveDisplayOnly;
            StartupToggle.IsOn = StartupManager.IsEnabled;

            HotkeyText.Text = Loc.Format(App.Current.HotkeyAvailable ? "Hotkey_Available" : "Hotkey_Taken",
                TrayHost.HotkeyDisplayName);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void Update(Action<AppSettings> change)
    {
        if (_syncing) return;
        change(Settings);
        App.Current.ApplySettings();
    }

    private void OnEnabledToggled(object sender, RoutedEventArgs e) =>
        Update(s => s.Enabled = EnabledToggle.IsOn);

    private void OnIntensityChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        IntensityText.Text = $"{(int)e.NewValue}%";
        Update(s => s.Intensity = (int)e.NewValue);
    }

    private void OnColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        ColorSwatch.Background = new SolidColorBrush(args.NewColor);
        Update(s => s.Color = AppSettings.FormatColor(args.NewColor.R, args.NewColor.G, args.NewColor.B));
    }

    private void OnAnimateToggled(object sender, RoutedEventArgs e)
    {
        FadePanel.Visibility = AnimateToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
        Update(s => s.Animate = AnimateToggle.IsOn);
    }

    private void OnFadeChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        FadeText.Text = $"{(int)e.NewValue} ms";
        Update(s => s.FadeDurationMs = (int)e.NewValue);
    }

    private void OnActiveDisplayToggled(object sender, RoutedEventArgs e) =>
        Update(s => s.ActiveDisplayOnly = ActiveDisplayToggle.IsOn);

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || LanguageCombo.SelectedItem is not ComboBoxItem { Tag: string language })
            return;
        Update(s => s.Language = language);

        // "System default" is itself translated, but a closed ComboBox doesn't redraw the selected
        // item's new text; reselecting it (outside this handler) refreshes the display.
        if (language.Length == 0)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _syncing = true;
                LanguageCombo.SelectedIndex = -1;
                LanguageCombo.SelectedIndex = 0;
                _syncing = false;
            });
        }
    }

    private void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        StartupManager.SetEnabled(StartupToggle.IsOn);
    }

    private void OnQuitClicked(object sender, RoutedEventArgs e) => App.Current.Shutdown();

    private static Color ToColor(string hex)
    {
        var (r, g, b) = AppSettings.ParseColor(hex);
        return ColorHelper.FromArgb(255, r, g, b);
    }
}
