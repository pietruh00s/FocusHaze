using Microsoft.Win32;

namespace WinDimmer.Core;

/// <summary>Registers the app in the current user's Run key so it starts (in the tray) with Windows.</summary>
internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WinDimmer";

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
}
