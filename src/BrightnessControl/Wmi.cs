using System;
using System.Collections.Generic;
using System.Management;

namespace BrightnessControl
{
    static class Wmi
    {
        public static Dictionary<string, string> MonitorNames()
        {
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var s = new ManagementObjectSearcher(@"root\wmi", "SELECT InstanceName, UserFriendlyName FROM WmiMonitorID"))
                foreach (ManagementObject o in s.Get())
                {
                    var chars = o["UserFriendlyName"] as ushort[];
                    string name = "";
                    if (chars != null) foreach (var c in chars) if (c != 0) name += (char)c;
                    names[(string)o["InstanceName"]] = name;
                }
            }
            catch (Exception e) { Log.Write("WmiMonitorID failed: " + e.Message); }
            return names;
        }

        public static List<KeyValuePair<string, int>> InternalPanels()
        {
            var list = new List<KeyValuePair<string, int>>();
            try
            {
                using (var s = new ManagementObjectSearcher(@"root\wmi", "SELECT InstanceName, CurrentBrightness FROM WmiMonitorBrightness"))
                foreach (ManagementObject o in s.Get())
                    list.Add(new KeyValuePair<string, int>((string)o["InstanceName"], Convert.ToInt32(o["CurrentBrightness"])));
            }
            // Desktops and most external monitors have no WmiMonitorBrightness instance; that's expected.
            catch (Exception e) { Log.Write("WmiMonitorBrightness: " + e.Message.Trim()); }
            return list;
        }

        public static bool TryGetBrightness(string instance, out int value)
        {
            foreach (var p in InternalPanels())
                if (string.Equals(p.Key, instance, StringComparison.OrdinalIgnoreCase)) { value = p.Value; return true; }
            value = 0;
            return false;
        }

        public static void SetBrightness(string instance, int value)
        {
            using (var s = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightnessMethods"))
            foreach (ManagementObject o in s.Get())
            {
                if (!string.Equals((string)o["InstanceName"], instance, StringComparison.OrdinalIgnoreCase)) continue;
                o.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)value });
            }
        }
    }
}
