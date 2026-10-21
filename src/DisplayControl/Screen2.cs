using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace DisplayControl
{
    // MCCS VCP codes the app uses.
    static class Vcp
    {
        public const byte Brightness = 0x10;
        public const byte Contrast = 0x12;
        public const byte ColorPreset = 0x14;
        public const byte Input = 0x60;
        public const byte RedGain = 0x16;
        public const byte GreenGain = 0x18;
        public const byte BlueGain = 0x1A;
    }

    // One screen Windows reports. Screens that can't be read yet stay listed and are re-checked later.
    // All monitor I/O for a screen runs on one background "pump" at a time, so DDC/CI requests never overlap.
    class Screen2
    {
        public string Id;              // stable key for settings, e.g. DISPLAY\MSI30C5\4&304d2cbe&0&UID4165
        public string Name;
        public string Kind;            // "DDC", "WMI", or "None" (Windows sees it but offers no way to control it)
        public IntPtr Handle;          // physical monitor handle for DDC
        public string WmiInstance;     // instance name for WMI
        public Rectangle Bounds;       // where it sits on the desktop (empty if unknown)
        public string Device;          // GDI device name, e.g. \\.\DISPLAY2 (for gamma ramps); null if unknown
        public volatile int Brightness;
        public volatile int Max = 100;

        // False while the screen hasn't answered a brightness read (still waking up, DDC/CI off, busy bus).
        public volatile bool Responding = true;

        // True when the monitor accepted the command but its brightness did not actually change
        // (DDC/CI writes blocked in the monitor's menu, or brightness locked by an ECO/picture mode).
        public volatile bool Unresponsive;

        // Parsed capabilities (DDC only); null until fetched.
        public volatile Capabilities Caps;

        // Raised on a background thread when Responding, Unresponsive, Brightness or Caps changes.
        public event Action<Screen2> StatusChanged;

        readonly object gate = new object();
        readonly Dictionary<byte, uint> pendingVcp = new Dictionary<byte, uint>();
        readonly Queue<Action> jobs = new Queue<Action>();
        int pending = -1;
        bool probe, busy, released;

        public bool Controllable { get { return Kind != "None"; } }
        public bool WritePending { get { lock (gate) return pending >= 0; } }

        // Nothing queued or running for this screen (used to wait for colour restores before exiting).
        public bool Idle { get { lock (gate) return !busy && pending < 0 && !probe && pendingVcp.Count == 0 && jobs.Count == 0; } }

        // Queue a brightness change; only the latest value is sent.
        public void Set(int value)
        {
            if (!Controllable) return;
            lock (gate)
            {
                if (released) return;
                pending = value;
            }
            Kick();
        }

        // Queue a write of any other VCP feature; only the latest value per code is sent.
        public void SetVcp(byte code, uint value)
        {
            if (Kind != "DDC") return;
            lock (gate)
            {
                if (released) return;
                pendingVcp[code] = value;
            }
            Kick();
        }

        // Queue a fresh read of the current brightness (and whether the screen answers at all).
        public void Refresh()
        {
            if (!Controllable) return;
            lock (gate)
            {
                if (released) return;
                probe = true;
            }
            Kick();
        }

        // Run work that talks to the monitor (use ReadVcp inside it) on this screen's pump.
        public void Run(Action job)
        {
            lock (gate)
            {
                if (released) return;
                jobs.Enqueue(job);
            }
            Kick();
        }

        // Free the monitor handle. If work is in flight, the pump frees it when it finishes.
        public void Release()
        {
            lock (gate)
            {
                if (released) return;
                released = true;
                if (busy) return;
                busy = true;   // claim the pump so a racing Kick() can't start one that destroys again
            }
            Destroy();
        }

        // Only call from inside a Run() job.
        public bool ReadVcp(byte code, out uint cur, out uint max)
        {
            for (int i = 1; i <= 2; i++)
            {
                if (Native.GetVCPFeatureAndVCPFeatureReply(Handle, code, IntPtr.Zero, out cur, out max)) return true;
                if (i < 2) Thread.Sleep(60);
            }
            Log.Write(Name + ": read of VCP 0x" + code.ToString("X2") + " failed, error 0x" + Marshal.GetLastWin32Error().ToString("X"));
            cur = max = 0;
            return false;
        }

        // Load capabilities from the settings cache, or fetch them from the monitor (slow) in the background.
        public void LoadCapabilities()
        {
            if (Kind != "DDC") return;
            var cached = Settings.Current.For(Id).Capabilities;
            if (!string.IsNullOrEmpty(cached)) { Caps = Capabilities.Parse(cached); return; }
            Run(delegate
            {
                string raw = FetchCapabilities();
                if (raw == null) return;
                Settings.Current.For(Id).Capabilities = raw;
                Settings.Current.Save();
                Caps = Capabilities.Parse(raw);
                Log.Write(Name + ": capabilities " + raw);
                Raise();
            });
        }

        string FetchCapabilities()
        {
            for (int attempt = 1; attempt <= 2; attempt++)
            {
                uint len;
                if (Native.GetCapabilitiesStringLength(Handle, out len) && len > 0)
                {
                    var buf = new byte[len];
                    if (Native.CapabilitiesRequestAndCapabilitiesReply(Handle, buf, len))
                        return Encoding.ASCII.GetString(buf).TrimEnd('\0');
                }
                Log.Write(Name + ": capabilities request " + attempt + "/2 failed, error 0x" + Marshal.GetLastWin32Error().ToString("X"));
                Thread.Sleep(200);
            }
            return null;
        }

        void Kick()
        {
            lock (gate)
            {
                if (busy) return;
                busy = true;
            }
            ThreadPool.QueueUserWorkItem(delegate { Pump(); });
        }

        void Destroy()
        {
            if (Kind == "DDC") Native.DestroyPhysicalMonitor(Handle);
        }

        // Brightness first (the user is waiting on it), then other writes, then reads and jobs.
        void Pump()
        {
            while (true)
            {
                int v = -1;
                bool doProbe = false;
                byte code = 0;
                uint codeValue = 0;
                bool doVcp = false;
                Action job = null;
                lock (gate)
                {
                    if (released) { busy = false; pending = -1; probe = false; pendingVcp.Clear(); jobs.Clear(); break; }
                    if (pending >= 0) { v = pending; pending = -1; }
                    else if (pendingVcp.Count > 0)
                    {
                        foreach (var kv in pendingVcp) { code = kv.Key; codeValue = kv.Value; break; }
                        pendingVcp.Remove(code);
                        doVcp = true;
                    }
                    else if (probe) { probe = false; doProbe = true; }
                    else if (jobs.Count > 0) job = jobs.Dequeue();
                    else { busy = false; return; }
                }
                try
                {
                    if (job != null) job();
                    else if (doProbe) Probe();
                    else if (doVcp)
                    {
                        if (!Native.SetVCPFeature(Handle, code, codeValue))
                            Log.Write(Name + ": write of VCP 0x" + code.ToString("X2") + " failed, error 0x" + Marshal.GetLastWin32Error().ToString("X"));
                    }
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
                if (!Native.SetVCPFeature(Handle, Vcp.Brightness, target))
                    Log.Write(Name + ": brightness write failed, error 0x" + Marshal.GetLastWin32Error().ToString("X"));
                bool last;
                lock (gate) last = pending < 0;
                if (last && Responding) CheckApplied(target);
            }
            else
            {
                LaptopWatcher.NoteOwnWrite();
                Wmi.SetBrightness(WmiInstance, v);
                LaptopWatcher.NoteOwnWrite();   // the event can arrive a little after the call returns
            }
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
            bool ok = Native.GetVCPFeatureAndVCPFeatureReply(Handle, Vcp.Brightness, IntPtr.Zero, out cur, out max);
            bool ignored = ok && Math.Abs((int)cur - (int)target) > 2;
            if (ignored == Unresponsive) return;
            Unresponsive = ignored;
            if (ignored) Log.Write(Name + ": accepted brightness " + target + " but reports " + cur);
            Raise();
        }

        public void Raise()
        {
            var handler = StatusChanged;
            if (handler != null) handler(this);
        }
    }
}
