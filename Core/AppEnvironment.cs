using System.Runtime.InteropServices;
using Microsoft.Windows.AppLifecycle;

namespace FocusHaze.Core;

/// <summary>How the app was installed and launched: Microsoft Store (MSIX) package or the zip release.</summary>
internal static class AppEnvironment
{
    private const int AppModelErrorNoPackage = 15700;

    /// <summary>True when running from an MSIX package (Microsoft Store), false for the zip release.</summary>
    public static bool IsPackaged { get; } = DetectPackaged();

    public static string DataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FocusHaze");

    /// <summary>True when Windows started the app at sign-in: "--tray" from the Run key (zip release)
    /// or a StartupTask activation (MSIX). The app then starts quietly in the tray.</summary>
    public static bool LaunchedAtStartup()
    {
        if (Environment.GetCommandLineArgs().Contains("--tray", StringComparer.OrdinalIgnoreCase))
            return true;
        if (!IsPackaged)
            return false;
        try
        {
            return AppInstance.GetCurrent().GetActivatedEventArgs().Kind == ExtendedActivationKind.StartupTask;
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "activation kind");
            return false;
        }
    }

    private static bool DetectPackaged()
    {
        int length = 0;
        return GetCurrentPackageFullName(ref length, null) != AppModelErrorNoPackage;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);
}
