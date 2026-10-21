using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;

namespace BrightnessControl
{
    static class Monitors
    {
        // "\\?\DISPLAY#MSI30C5#4&304d2cbe&0&UID4165#{guid}" -> "DISPLAY\MSI30C5\4&304d2cbe&0&UID4165"
        static string ToInstancePrefix(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return null;
            string s = deviceId.Replace(@"\\?\", "");
            int brace = s.IndexOf("#{");
            if (brace > 0) s = s.Substring(0, brace);
            return s.Replace('#', '\\');
        }

        static string ModelCode(string prefix)
        {
            if (prefix == null) return "";
            var parts = prefix.Split('\\');
            return parts.Length > 1 ? parts[1] : "";
        }

        // Read VCP 0x10 (brightness), retrying because many monitors miss the odd DDC/CI request,
        // especially right after waking up. Each failure is logged with its error code.
        public static bool ReadDdc(string name, IntPtr handle, int attempts, out uint cur, out uint max)
        {
            for (int i = 1; i <= attempts; i++)
            {
                if (Native.GetVCPFeatureAndVCPFeatureReply(handle, Vcp.Brightness, IntPtr.Zero, out cur, out max) && max > 0) return true;
                Log.Write(name + ": brightness read " + i + "/" + attempts + " failed, error 0x" + Marshal.GetLastWin32Error().ToString("X"));
                if (i < attempts) Thread.Sleep(60 * i);
            }
            cur = max = 0;
            return false;
        }

        // Lists every screen Windows reports. Screens that don't answer are still returned (Responding = false),
        // so they show up in the panel and get re-checked later instead of silently disappearing.
        // Slow (DDC/CI retries) - call it off the UI thread.
        public static List<Screen2> Detect()
        {
            Log.Write("--- detecting screens ---");
            var result = new List<Screen2>();
            var names = Wmi.MonitorNames();
            var internals = Wmi.InternalPanels();
            var usedWmi = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Log.Write("WMI: " + names.Count + " monitor IDs, " + internals.Count + " brightness panel(s)");

            var hmons = new List<IntPtr>();
            Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate(IntPtr h, IntPtr a, IntPtr b, IntPtr c) { hmons.Add(h); return true; }, IntPtr.Zero);
            Log.Write("Windows reports " + hmons.Count + " display(s)");

            foreach (var hmon in hmons)
            {
                var mi = new Native.MONITORINFOEX();
                mi.cbSize = Marshal.SizeOf(typeof(Native.MONITORINFOEX));
                Native.GetMonitorInfo(hmon, ref mi);
                var dd = new Native.DISPLAY_DEVICE();
                dd.cb = Marshal.SizeOf(typeof(Native.DISPLAY_DEVICE));
                Native.EnumDisplayDevices(mi.szDevice, 0, ref dd, 1);
                string prefix = ToInstancePrefix(dd.DeviceID);

                string friendly = null, wmiInstance = null;
                if (prefix != null)
                    foreach (var kv in names)
                        if (kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { friendly = kv.Value; wmiInstance = kv.Key; break; }
                if (string.IsNullOrEmpty(friendly)) friendly = ModelCode(prefix);
                if (string.IsNullOrEmpty(friendly)) friendly = "Display";
                Log.Write(mi.szDevice + " '" + friendly + "' " + (prefix ?? "(no device ID)"));
                var bounds = Rectangle.FromLTRB(mi.rcLeft, mi.rcTop, mi.rcRight, mi.rcBottom);
                string id = prefix ?? mi.szDevice;

                // Laptop panel: controlled through WMI.
                bool handledByWmi = false;
                if (wmiInstance != null)
                    foreach (var p in internals)
                        if (string.Equals(p.Key, wmiInstance, StringComparison.OrdinalIgnoreCase))
                        {
                            result.Add(new Screen2 { Id = prefix, Name = "Laptop screen", Kind = "WMI", WmiInstance = p.Key, Brightness = p.Value, Bounds = bounds, Device = mi.szDevice });
                            usedWmi.Add(p.Key);
                            handledByWmi = true;
                            Log.Write("  -> laptop screen via WMI, " + p.Value + "%");
                        }
                if (handledByWmi) continue;

                uint count;
                Native.PHYSICAL_MONITOR[] phys = null;
                if (!Native.GetNumberOfPhysicalMonitorsFromHMONITOR(hmon, out count) || count == 0)
                    Log.Write("  -> no physical monitor handle (error 0x" + Marshal.GetLastWin32Error().ToString("X") + ")");
                else
                {
                    phys = new Native.PHYSICAL_MONITOR[count];
                    if (!Native.GetPhysicalMonitorsFromHMONITOR(hmon, count, phys))
                    {
                        Log.Write("  -> couldn't open physical monitor (error 0x" + Marshal.GetLastWin32Error().ToString("X") + ")");
                        phys = null;
                    }
                }
                if (phys == null)
                {
                    result.Add(new Screen2 { Id = id, Name = friendly, Kind = "None", Responding = false, Brightness = 100, Bounds = bounds, Device = mi.szDevice });
                    continue;
                }

                for (int n = 0; n < phys.Length; n++)
                {
                    var pm = phys[n];
                    uint cur, max;
                    if (ReadDdc(friendly, pm.hPhysicalMonitor, 3, out cur, out max))
                    {
                        int pct = (int)Math.Round(cur * 100.0 / max);
                        result.Add(new Screen2 { Id = id + (phys.Length > 1 ? "#" + n : ""), Name = friendly, Kind = "DDC", Handle = pm.hPhysicalMonitor, Brightness = pct, Max = (int)max, Bounds = bounds, Device = mi.szDevice });
                        Log.Write("  -> DDC/CI, " + cur + "/" + max);
                    }
                    else
                    {
                        // Keep the handle: the monitor may answer once it has woken up, and writes may work even when reads don't.
                        result.Add(new Screen2 { Id = id + (phys.Length > 1 ? "#" + n : ""), Name = friendly, Kind = "DDC", Handle = pm.hPhysicalMonitor, Brightness = 50, Responding = false, Bounds = bounds, Device = mi.szDevice });
                        Log.Write("  -> DDC/CI not answering; kept, will retry later");
                    }
                }
            }

            // Any WMI panel not matched above (e.g. when display IDs differ).
            foreach (var p in internals)
                if (!usedWmi.Contains(p.Key))
                {
                    result.Add(new Screen2 { Id = p.Key, Name = "Laptop screen", Kind = "WMI", WmiInstance = p.Key, Brightness = p.Value });
                    Log.Write("Unmatched WMI panel " + p.Key + " added as laptop screen");
                }

            Log.Write("Found " + result.Count + " screen(s)");
            return result;
        }

        public static void Release(List<Screen2> screens)
        {
            if (screens == null) return;
            foreach (var s in screens) s.Release();
        }
    }
}
