using System;
using System.IO;

namespace BrightnessControl
{
    // Where the app runs from: an MSIX package (Microsoft Store) or a plain exe.
    static class AppInfo
    {
        const int AppModelErrorNoPackage = 15700;

        public static readonly bool IsPackaged = DetectPackaged();

        // Settings and the log live here. Packaged apps get their own folder; writing to %LOCALAPPDATA%
        // from a package is silently redirected, so other programs (e.g. Notepad) wouldn't find the files.
        public static readonly string DataFolder = IsPackaged
            ? Windows.Storage.ApplicationData.Current.LocalFolder.Path
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BrightnessControl");

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
