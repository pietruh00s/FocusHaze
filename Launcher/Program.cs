using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WinDimmer.Launcher
{
    /// <summary>
    /// Tiny .NET Framework stub that sits in the root of the release folder and starts the real app
    /// from the "app" subfolder, so the root only contains this exe and the license.
    /// .NET Framework 4.8 ships with every supported Windows version, so this needs no extra runtime.
    /// </summary>
    internal static class Program
    {
        private const string AppFolder = "app";
        private const string AppExe = "WinDimmer.exe";

        [STAThread]
        private static int Main(string[] args)
        {
            string target = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppFolder, AppExe);
            if (!File.Exists(target))
            {
                MessageBox(IntPtr.Zero,
                    "Can't find " + Path.Combine(AppFolder, AppExe) + ".\n\nExtract the whole WinDimmer folder from the zip and try again.",
                    "WinDimmer", 0x10 /* MB_ICONERROR */);
                return 1;
            }

            Process.Start(new ProcessStartInfo(target, JoinArguments(args))
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(target),
            });
            return 0;
        }

        private static string JoinArguments(string[] args)
        {
            var builder = new StringBuilder();
            foreach (string arg in args)
            {
                if (builder.Length > 0)
                    builder.Append(' ');
                builder.Append(arg.IndexOfAny(new[] { ' ', '\t', '"' }) >= 0
                    ? "\"" + arg.Replace("\"", "\\\"") + "\""
                    : arg);
            }
            return builder.ToString();
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
    }
}
