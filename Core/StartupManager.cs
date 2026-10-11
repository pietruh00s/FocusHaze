using Microsoft.Win32;
using Windows.ApplicationModel;

namespace FocusHaze.Core;

internal enum StartupState
{
    Disabled,
    Enabled,
    /// <summary>Turned off in Windows Settings › Apps › Startup (or Task Manager); only the user can turn it back on there.</summary>
    BlockedByUser,
    /// <summary>Turned off by group policy.</summary>
    BlockedByPolicy,
}

/// <summary>
/// "Start with Windows". The zip release uses the current user's Run key; the Microsoft Store (MSIX) package
/// uses its declared StartupTask, the supported mechanism for packaged apps.
/// </summary>
internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "FocusHaze";

    // Run value written by versions up to 1.0.3, when the app was called "WinDimmer".
    private const string LegacyValueName = "WinDimmer";

    // Must match the TaskId of the StartupTask extension in Package.appxmanifest.
    private const string StartupTaskId = "FocusHazeStartup";

    private static string Command => $"\"{Environment.ProcessPath}\" --tray";

    public static async Task<StartupState> GetStateAsync()
    {
        if (AppEnvironment.IsPackaged)
            return Map((await StartupTask.GetAsync(StartupTaskId)).State);

        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is string value && string.Equals(value, Command, StringComparison.OrdinalIgnoreCase)
            ? StartupState.Enabled
            : StartupState.Disabled;
    }

    /// <summary>Requests the change and returns the resulting state, which may differ (e.g. blocked by the user).</summary>
    public static async Task<StartupState> SetEnabledAsync(bool enabled)
    {
        if (AppEnvironment.IsPackaged)
        {
            StartupTask task = await StartupTask.GetAsync(StartupTaskId);
            if (enabled)
                return Map(await task.RequestEnableAsync());
            task.Disable();
            return Map(task.State);
        }

        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
            key.SetValue(ValueName, Command);
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        return enabled ? StartupState.Enabled : StartupState.Disabled;
    }

    /// <summary>Carries "start with Windows" over from the old app name to this executable (zip release only).</summary>
    public static void MigrateLegacy()
    {
        if (AppEnvironment.IsPackaged)
            return;
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (key.GetValue(LegacyValueName) is null)
            return;
        key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
        key.SetValue(ValueName, Command);
    }

    private static StartupState Map(StartupTaskState state) => state switch
    {
        StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy => StartupState.Enabled,
        StartupTaskState.DisabledByUser => StartupState.BlockedByUser,
        StartupTaskState.DisabledByPolicy => StartupState.BlockedByPolicy,
        _ => StartupState.Disabled,
    };
}
