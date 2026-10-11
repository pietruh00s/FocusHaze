using System.Text;

namespace FocusHaze.Core;

/// <summary>
/// Appends unexpected exceptions to a small local log (%LOCALAPPDATA%\FocusHaze\error.log) so a failure in
/// a window or event callback is recorded instead of taking the app down. Nothing is ever sent anywhere.
/// </summary>
internal static class CrashLog
{
    private const long MaxBytes = 256 * 1024;
    private static readonly object Gate = new();

    private static string FilePath => Path.Combine(AppEnvironment.DataFolder, "error.log");

    public static void Write(Exception exception, string context)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppEnvironment.DataFolder);
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > MaxBytes)
                    file.Delete();

                var entry = new StringBuilder()
                    .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append(" [").Append(context).AppendLine("]")
                    .AppendLine(exception.ToString())
                    .AppendLine();
                File.AppendAllText(FilePath, entry.ToString());
            }
        }
        catch
        {
            // Logging must never become the failure.
        }
    }
}
