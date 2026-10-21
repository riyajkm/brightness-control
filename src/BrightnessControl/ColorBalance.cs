using System;
using System.Collections.Generic;
using System.Threading;

namespace BrightnessControl
{
    // Colour matching: a per-screen white balance (warmth and tint, -50..50) so whites look alike across screens.
    // Monitors that accept RGB gain over DDC/CI are adjusted in hardware (best quality). Everything else - laptop
    // panels, and monitors whose firmware ignores gain writes (the MSI MD342CQP does) - is adjusted through the
    // display's gamma ramp. Channels are only ever turned down, so nothing clips.
    static class ColorBalance
    {
        // Warmth > 0 is warmer (less blue), tint > 0 is more magenta (less green).
        public static void Multipliers(int warmth, int tint, out double r, out double g, out double b)
        {
            r = g = b = 1.0;
            if (warmth > 0) { b -= warmth * 0.006; g -= warmth * 0.0015; }
            else { r -= -warmth * 0.006; g -= -warmth * 0.0015; }
            if (tint > 0) g -= tint * 0.005;
            else { r -= -tint * 0.0025; b -= -tint * 0.0025; }
            double top = Math.Max(r, Math.Max(g, b));
            r = Math.Max(0.5, r / top); g = Math.Max(0.5, g / top); b = Math.Max(0.5, b / top);
        }

        public static bool UsesGains(Screen2 s)
        {
            var caps = s.Caps;
            return s.Kind == "DDC" && s.Responding && caps != null && !Settings.Current.For(s.Id).GainsUnsupported
                && caps.Has(Vcp.RedGain) && caps.Has(Vcp.GreenGain) && caps.Has(Vcp.BlueGain);
        }

        // Software (gamma) colour: screens with no usable RGB gain. A DDC monitor only qualifies once we know
        // it has none - by its capabilities, or because it ignored gain writes. One whose capabilities haven't
        // loaded, or that lists RGB gain but isn't answering right now, is left alone: it may take hardware gain
        // in a moment, and adjusting it both ways would double the correction.
        public static bool UsesSoftware(Screen2 s)
        {
            if (string.IsNullOrEmpty(s.Device) || !s.Controllable) return false;
            if (s.Kind != "DDC" || Settings.Current.For(s.Id).GainsUnsupported) return true;
            var caps = s.Caps;
            return caps != null && !(caps.Has(Vcp.RedGain) && caps.Has(Vcp.GreenGain) && caps.Has(Vcp.BlueGain));
        }

        public static bool CanAdjust(Screen2 s)
        {
            return UsesGains(s) || UsesSoftware(s);
        }

        public static string Describe(Screen2 s)
        {
            if (UsesGains(s)) return "Adjusts the monitor's own colour settings";
            if (s.Kind == "DDC" && Settings.Current.For(s.Id).GainsUnsupported) return "This monitor ignores colour commands, so Windows adjusts its colour";
            return "Windows adjusts this screen's colour";
        }

        // Before the first hardware change: remember the monitor's preset and gains, and check it really takes
        // gain writes. Runs on the screen's pump; calls done() on that thread when finished.
        public static void Prepare(Screen2 s, Action done)
        {
            if (!UsesGains(s)) { done(); return; }
            s.Run(delegate
            {
                var m = Settings.Current.For(s.Id);
                try
                {
                    uint cur, max;
                    if (m.OriginalPreset == null && s.ReadVcp(Vcp.ColorPreset, out cur, out max)) m.OriginalPreset = (int)(cur & 0xFF);
                    var gains = new int[3];
                    var maxes = new int[3];
                    byte[] codes = { Vcp.RedGain, Vcp.GreenGain, Vcp.BlueGain };
                    bool ok = true;
                    for (int i = 0; i < 3; i++)
                    {
                        if (s.ReadVcp(codes[i], out cur, out max) && max > 0) { gains[i] = (int)cur; maxes[i] = (int)max; }
                        else ok = false;
                    }
                    if (!ok) { MarkUnsupported(s, m, "couldn't read RGB gain"); return; }
                    if (m.OriginalGains == null) m.OriginalGains = gains;
                    m.GainMax = maxes;

                    // Write the current balance and read it back: some firmware accepts the command and ignores it.
                    var target = Targets(m, m.Warmth, m.Tint);
                    for (int i = 0; i < 3; i++) Native.SetVCPFeature(s.Handle, codes[i], (uint)target[i]);
                    Thread.Sleep(700);
                    for (int i = 0; i < 3; i++)
                        if (!s.ReadVcp(codes[i], out cur, out max) || Math.Abs((int)cur - target[i]) > 2)
                        {
                            MarkUnsupported(s, m, "ignored an RGB gain write (asked " + target[i] + ", reads " + cur + ")");
                            RestoreHardware(s, m);   // it may have taken some of the writes
                            return;
                        }
                    Log.Write(s.Name + ": colour matching uses the monitor's RGB gain");
                }
                finally
                {
                    Settings.Current.Save();
                    done();
                }
            });
        }

        static void MarkUnsupported(Screen2 s, MonitorSettings m, string why)
        {
            m.GainsUnsupported = true;
            Log.Write(s.Name + ": " + why + "; colour matching will use Windows colour output instead");
        }

        // Built on the monitor's own original gains, so a neutral balance writes back exactly what it had.
        static int[] Targets(MonitorSettings m, int warmth, int tint)
        {
            double r, g, b;
            Multipliers(warmth, tint, out r, out g, out b);
            var max = m.GainMax ?? new[] { 100, 100, 100 };
            var basis = new int[3];
            for (int i = 0; i < 3; i++)
                basis[i] = m.OriginalGains != null && m.OriginalGains[i] > 0 ? m.OriginalGains[i] : max[i];
            return new[] { (int)Math.Round(basis[0] * r), (int)Math.Round(basis[1] * g), (int)Math.Round(basis[2] * b) };
        }

        // Live change from the wizard (UI thread). Hardware writes are coalesced by the screen's pump.
        public static void Apply(Screen2 s, int warmth, int tint)
        {
            var m = Settings.Current.For(s.Id);
            if (UsesGains(s) && m.GainMax != null)
            {
                var t = Targets(m, warmth, tint);
                s.SetVcp(Vcp.RedGain, (uint)t[0]);
                s.SetVcp(Vcp.GreenGain, (uint)t[1]);
                s.SetVcp(Vcp.BlueGain, (uint)t[2]);
            }
            else if (UsesSoftware(s))
            {
                double r, g, b;
                Multipliers(warmth, tint, out r, out g, out b);
                SetGamma(s.Device, r, g, b);
            }
        }

        // Back to how the screen was before colour matching. persist = false only undoes it on screen (the
        // wizard's "Reset all", which Cancel can still take back); the saved values change on Save.
        public static void Reset(Screen2 s, bool persist = true)
        {
            var m = Settings.Current.For(s.Id);
            if (persist) { m.Warmth = m.Tint = 0; Settings.Current.Save(); }
            if (UsesSoftware(s)) SetGamma(s.Device, 1, 1, 1);
            if (s.Kind == "DDC")
                // Decided on the pump, after any Prepare still queued there has recorded the originals.
                s.Run(delegate
                {
                    if (m.OriginalPreset == null && m.OriginalGains == null) return;
                    RestoreHardware(s, m);
                    // Originals are kept: they're the monitor's true starting point for any later change.
                    Log.Write(s.Name + ": colours reset");
                });
        }

        // Gains live in the User preset's memory, and writing them switches many monitors (the ViewSonic VA2759
        // does) to User. So: put the original gains back first, then the preset, last.
        static void RestoreHardware(Screen2 s, MonitorSettings m)
        {
            if (m.OriginalGains != null)
            {
                Native.SetVCPFeature(s.Handle, Vcp.RedGain, (uint)m.OriginalGains[0]);
                Native.SetVCPFeature(s.Handle, Vcp.GreenGain, (uint)m.OriginalGains[1]);
                Native.SetVCPFeature(s.Handle, Vcp.BlueGain, (uint)m.OriginalGains[2]);
                Thread.Sleep(500);
            }
            RestorePreset(s, m);
        }

        // Monitors need about a second after a preset change before they listen again; retry until it reads back.
        static void RestorePreset(Screen2 s, MonitorSettings m)
        {
            if (m.OriginalPreset == null) return;
            for (int attempt = 1; attempt <= 4; attempt++)
            {
                uint cur, max;
                if (s.ReadVcp(Vcp.ColorPreset, out cur, out max) && (cur & 0xFF) == m.OriginalPreset.Value) return;
                Native.SetVCPFeature(s.Handle, Vcp.ColorPreset, (uint)m.OriginalPreset.Value);
                Thread.Sleep(800 * attempt);
            }
            Log.Write(s.Name + ": couldn't restore colour preset 0x" + m.OriginalPreset.Value.ToString("X2"));
        }

        // Gamma ramps reset on display changes and can be overwritten by other software, so re-apply them
        // after every detection and when the panel opens. Hardware gains live in the monitor and persist.
        public static void ReapplySoftware(IEnumerable<Screen2> screens)
        {
            foreach (var s in screens)
            {
                var m = Settings.Current.For(s.Id);
                if ((m.Warmth != 0 || m.Tint != 0) && UsesSoftware(s))
                {
                    double r, g, b;
                    Multipliers(m.Warmth, m.Tint, out r, out g, out b);
                    SetGamma(s.Device, r, g, b);
                }
            }
        }

        // On exit: software colour only stays right while the app can re-apply it, so take it off. It comes back
        // when the app starts again. Hardware gains stay in the monitor.
        public static void RemoveSoftware(IEnumerable<Screen2> screens)
        {
            foreach (var s in screens)
            {
                var m = Settings.Current.For(s.Id);
                if ((m.Warmth != 0 || m.Tint != 0) && UsesSoftware(s)) SetGamma(s.Device, 1, 1, 1);
            }
        }

        public static bool SetGamma(string device, double r, double g, double b)
        {
            IntPtr dc = Native.CreateDC(null, device, null, IntPtr.Zero);
            if (dc == IntPtr.Zero) return false;
            try
            {
                var ramp = new ushort[768];
                for (int i = 0; i < 256; i++)
                {
                    ramp[i] = (ushort)Math.Round(i * 257 * r);
                    ramp[256 + i] = (ushort)Math.Round(i * 257 * g);
                    ramp[512 + i] = (ushort)Math.Round(i * 257 * b);
                }
                bool ok = Native.SetDeviceGammaRamp(dc, ramp);
                if (!ok) Log.Write(device + ": Windows refused the colour adjustment");
                return ok;
            }
            finally { Native.DeleteDC(dc); }
        }
    }
}
