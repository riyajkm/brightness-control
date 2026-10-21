using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace BrightnessControl
{
    // One screen Windows reports. Screens that can't be read yet stay listed and are re-checked later.
    class Screen2
    {
        public string Name;
        public string Kind;            // "DDC", "WMI", or "None" (Windows sees it but offers no way to control it)
        public IntPtr Handle;          // physical monitor handle for DDC
        public string WmiInstance;     // instance name for WMI
        public volatile int Brightness;
        public volatile int Max = 100;

        // False while the screen hasn't answered a brightness read (still waking up, DDC/CI off, busy bus).
        public volatile bool Responding = true;

        // True when the monitor accepted the command but its brightness did not actually change
        // (DDC/CI writes blocked in the monitor's menu, or brightness locked by an ECO/picture mode).
        public volatile bool Unresponsive;

        // Raised on a background thread when Responding, Unresponsive or Brightness changes.
        public event Action<Screen2> StatusChanged;

        readonly object gate = new object();
        int pending = -1;
        bool probe, busy, released;

        public bool Controllable { get { return Kind != "None"; } }
        public bool WritePending { get { lock (gate) return pending >= 0; } }

        // Queue a brightness change; only the latest value is sent, on a background thread.
        public void Set(int value)
        {
            if (!Controllable) return;
            lock (gate)
            {
                if (released) return;
                pending = value;
                if (busy) return;
                busy = true;
            }
            ThreadPool.QueueUserWorkItem(delegate { Pump(); });
        }

        // Queue a fresh read of the current brightness (and whether the screen answers at all).
        public void Refresh()
        {
            if (!Controllable) return;
            lock (gate)
            {
                if (released) return;
                probe = true;
                if (busy) return;
                busy = true;
            }
            ThreadPool.QueueUserWorkItem(delegate { Pump(); });
        }

        // Free the monitor handle. If a write or read is in flight, the pump frees it when it finishes.
        public void Release()
        {
            lock (gate)
            {
                if (released) return;
                released = true;
                if (busy) return;
            }
            Destroy();
        }

        void Destroy()
        {
            if (Kind == "DDC") Native.DestroyPhysicalMonitor(Handle);
        }

        void Pump()
        {
            while (true)
            {
                int v = -1;
                bool doProbe = false;
                lock (gate)
                {
                    if (released) { busy = false; pending = -1; probe = false; break; }
                    if (pending >= 0) { v = pending; pending = -1; }
                    else if (probe) { probe = false; doProbe = true; }
                    else { busy = false; return; }
                }
                try
                {
                    if (doProbe) Probe();
                    else Write(v);
                }
                catch (Exception e) { Log.Write(Name + ": " + e.Message); }
            }
            Destroy();
        }

        void Write(int v)
        {
            if (Kind == "DDC")
            {
                uint target = (uint)Math.Round(v * Max / 100.0);
                if (!Native.SetVCPFeature(Handle, 0x10, target))
                    Log.Write(Name + ": brightness write failed, error 0x" + Marshal.GetLastWin32Error().ToString("X"));
                bool last;
                lock (gate) last = pending < 0;
                if (last && Responding) CheckApplied(target);
            }
            else Wmi.SetBrightness(WmiInstance, v);
            Brightness = v;
        }

        void Probe()
        {
            int value = 0;
            bool ok;
            if (Kind == "DDC")
            {
                uint cur, max;
                ok = Monitors.ReadDdc(Name, Handle, 2, out cur, out max);
                if (ok) { Max = (int)max; value = (int)Math.Round(cur * 100.0 / max); }
            }
            else ok = Wmi.TryGetBrightness(WmiInstance, out value);

            bool was = Responding;
            Responding = ok;
            if (ok != was) Log.Write(Name + (ok ? ": now responding" : ": stopped responding"));
            if (!ok && !was) return;
            if (ok) Brightness = value;
            Raise();
        }

        // Read the value back after a short delay; if the monitor didn't take it, flag the screen.
        void CheckApplied(uint target)
        {
            Thread.Sleep(150);
            uint cur, max;
            bool ok = Native.GetVCPFeatureAndVCPFeatureReply(Handle, 0x10, IntPtr.Zero, out cur, out max);
            bool ignored = ok && Math.Abs((int)cur - (int)target) > 2;
            if (ignored == Unresponsive) return;
            Unresponsive = ignored;
            if (ignored) Log.Write(Name + ": accepted brightness " + target + " but reports " + cur);
            Raise();
        }

        void Raise()
        {
            var handler = StatusChanged;
            if (handler != null) handler(this);
        }
    }
}
