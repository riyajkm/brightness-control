using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Win32;

namespace DisplayControl
{
    // What the installer asks of the app: close cleanly, and before uninstalling, put monitors back as they were.
    static class Lifecycle
    {
        public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string RunName = "DisplayControl";

        public static bool HasColour(Screen2 s)
        {
            var m = Settings.Current.For(s.Id);
            return m.Warmth != 0 || m.Tint != 0;
        }

        // Starts colour resets for every matched screen. Returns false if there was nothing to reset.
        public static bool ResetAllColours(IEnumerable<Screen2> screens)
        {
            bool any = false;
            foreach (var s in screens)
                if (HasColour(s)) { ColorBalance.Reset(s); any = true; }
            if (any) Log.Write("Uninstall: resetting matched colours");
            return any;
        }

        public static bool AllIdle(IEnumerable<Screen2> screens)
        {
            foreach (var s in screens) if (!s.Idle) return false;
            return true;
        }

        public static void RemoveStartup()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                    if (k != null && k.GetValue(RunName) != null) k.DeleteValue(RunName, false);
            }
            catch (Exception e) { Log.Write("Couldn't remove Start with Windows: " + e.Message); }
        }

        // "--uninstall" when the app isn't running: no tray, no panel. Find the screens, reset any matched
        // colours (monitor restores take a few seconds), remove the startup entry, and exit.
        public static void UninstallWithoutUi()
        {
            Log.Write("Uninstall: app wasn't running, cleaning up without UI");
            var screens = Monitors.Detect();
            foreach (var s in screens) s.LoadCapabilities();
            if (ResetAllColours(screens))
                for (int waited = 0; waited < 20000 && !AllIdle(screens); waited += 200) Thread.Sleep(200);
            Monitors.Release(screens);
            RemoveStartup();
        }
    }
}
