using System;
using System.Management;

namespace BrightnessControl
{
    // Watches the laptop screen's brightness (brightness keys, Windows adaptive brightness, battery saver)
    // so external monitors can follow it.
    static class LaptopWatcher
    {
        static readonly object gate = new object();
        static ManagementEventWatcher watcher;
        static DateTime lastOwnWrite = DateTime.MinValue;

        // Raised on a WMI thread with the laptop's new brightness, for changes the app didn't make itself.
        public static event Action<int> Changed;

        // Our own WMI writes raise the same event. Ignore events shortly after them, or following
        // would feed back into itself (the laptop slider moves externals, which move the laptop...).
        public static void NoteOwnWrite()
        {
            lock (gate) lastOwnWrite = DateTime.UtcNow;
        }

        public static void Start()
        {
            lock (gate)
            {
                if (watcher != null) return;
                try
                {
                    watcher = new ManagementEventWatcher(new ManagementScope(@"root\wmi"), new EventQuery("SELECT * FROM WmiMonitorBrightnessEvent"));
                    watcher.EventArrived += OnEvent;
                    watcher.Start();
                    Log.Write("Watching laptop brightness changes");
                }
                catch (Exception e)
                {
                    Log.Write("Can't watch laptop brightness: " + e.Message);
                    watcher = null;
                }
            }
        }

        public static void Stop()
        {
            lock (gate)
            {
                if (watcher == null) return;
                try { watcher.Stop(); watcher.Dispose(); } catch { }
                watcher = null;
            }
        }

        static void OnEvent(object sender, EventArrivedEventArgs e)
        {
            int value;
            try { value = Convert.ToInt32(e.NewEvent["Brightness"]); }
            catch { return; }
            lock (gate)
                if ((DateTime.UtcNow - lastOwnWrite).TotalMilliseconds < 1500) return;
            var handler = Changed;
            if (handler != null) handler(value);
        }
    }
}
