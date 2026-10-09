using Microsoft.Win32;

namespace FocusHaze.Core;

/// <summary>Registers the app in the current user's Run key so it starts (in the tray) with Windows.</summary>
internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "FocusHaze";

    // Run value written by versions up to 1.0.3, when the app was called "WinDimmer".
    private const string LegacyValueName = "WinDimmer";

    private static string Command => $"\"{Environment.ProcessPath}\" --tray";

    public static bool IsEnabled
    {
        get
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string value
                && string.Equals(value, Command, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
            key.SetValue(ValueName, Command);
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>Carries "start with Windows" over from the old app name to this executable.</summary>
    public static void MigrateLegacy()
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (key.GetValue(LegacyValueName) is null)
            return;
        key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
        key.SetValue(ValueName, Command);
    }
}
