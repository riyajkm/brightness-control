using System;
using System.IO;

namespace DisplayControl
{
    // Where the app runs from: an MSIX package (Microsoft Store) or a plain exe.
    static class AppInfo
    {
        const int AppModelErrorNoPackage = 15700;

        public static readonly bool IsPackaged = DetectPackaged();

        // Settings and the log live here. Packaged apps get their own folder; writing to %LOCALAPPDATA%
        // from a package is silently redirected, so other programs (e.g. Notepad) wouldn't find the files.
        public static readonly string DataFolder = ChooseDataFolder();

        static string ChooseDataFolder()
        {
            if (IsPackaged)
            {
                // Never let this stop the app from starting: Log and Settings both depend on it.
                try { return Windows.Storage.ApplicationData.Current.LocalFolder.Path; }
                catch { }
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DisplayControl");
        }

        // The app was called "Brightness Control" before 1.0.1. Bring its settings (calibrations, colour
        // matching, cached capabilities) and log across once, so updating doesn't lose anything.
        public static void MigrateFromBrightnessControl()
        {
            if (IsPackaged) return;
            try
            {
                string old = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BrightnessControl");
                if (!Directory.Exists(old) || File.Exists(Path.Combine(DataFolder, "settings.json"))) return;
                Directory.CreateDirectory(DataFolder);
                foreach (var name in new[] { "settings.json", "detect.log" })
                {
                    string from = Path.Combine(old, name);
                    if (File.Exists(from)) File.Copy(from, Path.Combine(DataFolder, name), false);
                }
                File.AppendAllText(Path.Combine(DataFolder, "detect.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff  ") + "Settings moved from " + old + Environment.NewLine);
            }
            catch { }   // worst case the user starts with fresh settings
        }

        static bool DetectPackaged()
        {
            try
            {
                int length = 0;
                return Native.GetCurrentPackageFullName(ref length, null) != AppModelErrorNoPackage;
            }
            catch { return false; }   // API missing on very old Windows
        }
    }
}
