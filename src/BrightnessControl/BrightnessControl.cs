// Brightness Control - tray app for adjusting monitor brightness on Windows.
// External monitors use DDC/CI (dxva2.dll); the laptop panel uses WMI.
// Built with .NET 10 (Windows Forms).

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BrightnessControl
{
    static class Native
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szPhysicalMonitorDescription;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MONITORINFOEX
        {
            public int cbSize;
            public int rcLeft, rcTop, rcRight, rcBottom;
            public int wkLeft, wkTop, wkRight, wkBottom;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            public uint StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        }

        public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr lprc, IntPtr data);

        [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc cb, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX info);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool EnumDisplayDevices(string device, uint index, ref DISPLAY_DEVICE dd, uint flags);
        [DllImport("dxva2.dll")] public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint count);
        [DllImport("dxva2.dll")] public static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint count, [Out] PHYSICAL_MONITOR[] monitors);
        [DllImport("dxva2.dll")] public static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);
        [DllImport("dxva2.dll")] public static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr hMonitor, byte code, IntPtr type, out uint current, out uint max);
        [DllImport("dxva2.dll")] public static extern bool SetVCPFeature(IntPtr hMonitor, byte code, uint value);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr handle);
    }

    // One controllable screen.
    class Screen2
    {
        public string Name;
        public string Kind;            // "DDC" or "WMI"
        public IntPtr Handle;          // physical monitor handle for DDC
        public string WmiInstance;     // instance name for WMI
        public int Brightness;
        public int Max = 100;

        // True when the monitor accepted the command but its brightness did not actually change
        // (DDC/CI writes blocked in the monitor's menu, or brightness locked by an ECO/picture mode).
        public volatile bool Unresponsive;
        public event Action<Screen2> StatusChanged;

        readonly object gate = new object();
        int pending = -1;
        bool busy;

        // Queue a brightness change; only the latest value is sent, on a background thread.
        public void Set(int value)
        {
            lock (gate)
            {
                pending = value;
                if (busy) return;
                busy = true;
            }
            ThreadPool.QueueUserWorkItem(delegate { Pump(); });
        }

        void Pump()
        {
            while (true)
            {
                int v;
                lock (gate)
                {
                    if (pending < 0) { busy = false; return; }
                    v = pending; pending = -1;
                }
                try
                {
                    if (Kind == "DDC")
                    {
                        uint target = (uint)Math.Round(v * Max / 100.0);
                        Native.SetVCPFeature(Handle, 0x10, target);
                        bool last;
                        lock (gate) last = pending < 0;
                        if (last) CheckApplied(target);
                    }
                    else Wmi.SetBrightness(WmiInstance, v);
                    Brightness = v;
                }
                catch { }
            }
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
            var handler = StatusChanged;
            if (handler != null) handler(this);
        }
    }

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
            catch { }
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
            catch { }
            return list;
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

        public static List<Screen2> Detect()
        {
            var result = new List<Screen2>();
            var names = Wmi.MonitorNames();
            var internals = Wmi.InternalPanels();
            var usedWmi = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var hmons = new List<IntPtr>();
            Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate(IntPtr h, IntPtr a, IntPtr b, IntPtr c) { hmons.Add(h); return true; }, IntPtr.Zero);

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

                // Laptop panel: controlled through WMI.
                bool handledByWmi = false;
                if (wmiInstance != null)
                    foreach (var p in internals)
                        if (string.Equals(p.Key, wmiInstance, StringComparison.OrdinalIgnoreCase))
                        {
                            result.Add(new Screen2 { Name = "Laptop screen", Kind = "WMI", WmiInstance = p.Key, Brightness = p.Value });
                            usedWmi.Add(p.Key);
                            handledByWmi = true;
                        }
                if (handledByWmi) continue;

                uint count;
                if (!Native.GetNumberOfPhysicalMonitorsFromHMONITOR(hmon, out count) || count == 0) continue;
                var phys = new Native.PHYSICAL_MONITOR[count];
                if (!Native.GetPhysicalMonitorsFromHMONITOR(hmon, count, phys)) continue;
                foreach (var pm in phys)
                {
                    uint cur, max;
                    if (Native.GetVCPFeatureAndVCPFeatureReply(pm.hPhysicalMonitor, 0x10, IntPtr.Zero, out cur, out max) && max > 0)
                    {
                        int pct = (int)Math.Round(cur * 100.0 / max);
                        result.Add(new Screen2 { Name = friendly, Kind = "DDC", Handle = pm.hPhysicalMonitor, Brightness = pct, Max = (int)max });
                    }
                    else Native.DestroyPhysicalMonitor(pm.hPhysicalMonitor);
                }
            }

            // Any WMI panel not matched above (e.g. when display IDs differ).
            foreach (var p in internals)
                if (!usedWmi.Contains(p.Key))
                    result.Add(new Screen2 { Name = "Laptop screen", Kind = "WMI", WmiInstance = p.Key, Brightness = p.Value });

            return result;
        }

        public static void Release(List<Screen2> screens)
        {
            if (screens == null) return;
            foreach (var s in screens) if (s.Kind == "DDC") Native.DestroyPhysicalMonitor(s.Handle);
        }
    }

    class Theme
    {
        public static readonly Color Back = Color.FromArgb(32, 32, 32);
        public static readonly Color Card = Color.FromArgb(43, 43, 43);
        public static readonly Color Text = Color.FromArgb(240, 240, 240);
        public static readonly Color Muted = Color.FromArgb(160, 160, 160);
        public static readonly Color Accent = Color.FromArgb(76, 194, 255);
        public static readonly Color Track = Color.FromArgb(80, 80, 80);
    }

    // Flat slider drawn by hand so it looks at home on Windows 11's dark theme.
    class FlatSlider : Control
    {
        int value;
        bool dragging;
        public event EventHandler ValueChanged;

        public FlatSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = 28;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Value
        {
            get { return value; }
            set
            {
                int v = Math.Max(0, Math.Min(100, value));
                if (v == this.value) return;
                this.value = v;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        public void SetSilently(int v) { value = Math.Max(0, Math.Min(100, v)); Invalidate(); }

        int Pad { get { return 10; } }

        void FromX(int x)
        {
            float w = Width - Pad * 2;
            Value = (int)Math.Round(Math.Max(0, Math.Min(1, (x - Pad) / w)) * 100);
        }

        protected override void OnMouseDown(MouseEventArgs e) { dragging = true; Focus(); FromX(e.X); }
        protected override void OnMouseMove(MouseEventArgs e) { if (dragging) FromX(e.X); }
        protected override void OnMouseUp(MouseEventArgs e) { dragging = false; }
        protected override void OnMouseWheel(MouseEventArgs e) { Value += e.Delta > 0 ? 5 : -5; }
        protected override bool IsInputKey(Keys k) { return k == Keys.Left || k == Keys.Right || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) Value -= 5;
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) Value += 5;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Card);
            int cy = Height / 2;
            float w = Width - Pad * 2;
            float x = Pad + w * value / 100f;
            using (var p = new Pen(Theme.Track, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(p, Pad, cy, Pad + w, cy);
            using (var p = new Pen(Theme.Accent, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(p, Pad, cy, x, cy);
            using (var b = new SolidBrush(Color.FromArgb(69, 69, 69))) g.FillEllipse(b, x - 9, cy - 9, 18, 18);
            using (var b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, x - 5, cy - 5, 10, 10);
        }
    }

    class Panel : Form
    {
        readonly FlowLayoutPanel list;
        List<Screen2> screens;
        readonly List<FlatSlider> sliders = new List<FlatSlider>();
        readonly List<Label> values = new List<Label>();
        bool linking;

        public Panel()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Back;
            Font = new Font("Segoe UI", 9.5f);
            Width = 340;
            Padding = new Padding(1);

            list = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Theme.Back,
                Padding = new Padding(12, 12, 12, 8),
                AutoSize = false
            };
            Controls.Add(list);
            Deactivate += delegate { LastHidden = DateTime.Now; Hide(); };
        }

        public DateTime LastHidden = DateTime.MinValue;

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; /* drop shadow */ return cp; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Color.FromArgb(70, 70, 70))) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }

        Label MakeLabel(string text, Color color, float size, bool bold)
        {
            return new Label
            {
                Text = text,
                ForeColor = color,
                Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
                AutoSize = true,
                Margin = new Padding(2, 0, 0, 2)
            };
        }

        Button MakeButton(string text, EventHandler onClick)
        {
            var b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                Height = 30,
                Width = 69,
                Margin = new Padding(0, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 60);
            b.Click += onClick;
            return b;
        }

        public void Build(List<Screen2> found)
        {
            screens = found;
            list.SuspendLayout();
            list.Controls.Clear();
            sliders.Clear();
            values.Clear();
            allSlider = null;
            allValue = null;
            int inner = Width - 26;

            var title = MakeLabel("Brightness", Theme.Text, 12f, true);
            title.Margin = new Padding(2, 0, 0, 10);
            list.Controls.Add(title);

            if (screens.Count == 0)
            {
                var none = MakeLabel("No adjustable screens found.\nRight-click the tray icon and choose Refresh.", Theme.Muted, 9.5f, false);
                none.Margin = new Padding(2, 0, 0, 12);
                list.Controls.Add(none);
            }

            // "All screens" slider, shown when there is more than one screen.
            if (screens.Count > 1)
                AddRow("All screens", Average(), inner, true, -1);

            for (int i = 0; i < screens.Count; i++)
                AddRow(screens[i].Name, screens[i].Brightness, inner, false, i);

            var presets = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0, 8, 0, 4), BackColor = Theme.Back };
            presets.Controls.Add(MakeButton("Night", delegate { SetAll(20); }));
            presets.Controls.Add(MakeButton("Evening", delegate { SetAll(45); }));
            presets.Controls.Add(MakeButton("Day", delegate { SetAll(75); }));
            presets.Controls.Add(MakeButton("Max", delegate { SetAll(100); }));
            list.Controls.Add(presets);

            list.ResumeLayout();
            Height = list.PreferredSize.Height + 4;
        }

        int Average()
        {
            if (screens.Count == 0) return 50;
            int sum = 0;
            foreach (var s in screens) sum += s.Brightness;
            return sum / screens.Count;
        }

        void AddRow(string name, int value, int width, bool isAll, int index)
        {
            var card = new System.Windows.Forms.Panel { Width = width, Height = 62, BackColor = Theme.Card, Margin = new Padding(0, 0, 0, 8) };
            var nameLabel = MakeLabel(name, isAll ? Theme.Accent : Theme.Text, 9.5f, isAll);
            nameLabel.Location = new Point(10, 8);
            var valueLabel = MakeLabel(value + "%", Theme.Muted, 9.5f, false);
            valueLabel.AutoSize = false;
            valueLabel.TextAlign = ContentAlignment.MiddleRight;
            valueLabel.Size = new Size(60, 20);
            valueLabel.Location = new Point(width - 70, 8);
            var slider = new FlatSlider { Width = width - 8, Location = new Point(4, 30) };
            slider.SetSilently(value);
            card.Controls.Add(nameLabel);
            card.Controls.Add(valueLabel);
            card.Controls.Add(slider);
            list.Controls.Add(card);

            if (isAll)
            {
                slider.ValueChanged += delegate
                {
                    valueLabel.Text = slider.Value + "%";
                    if (linking) return;
                    linking = true;
                    for (int i = 0; i < sliders.Count; i++) sliders[i].Value = slider.Value;
                    linking = false;
                };
                allSlider = slider; allValue = valueLabel;
            }
            else
            {
                sliders.Add(slider);
                values.Add(valueLabel);
                var s = screens[index];

                // Warning shown when the monitor ignores brightness commands.
                var warn = MakeLabel("Monitor isn't accepting changes. In its own menu, turn DDC/CI on and set ECO Mode to Standard.", Color.FromArgb(255, 185, 90), 8.5f, false);
                warn.AutoSize = false;
                warn.Size = new Size(width - 20, 34);
                warn.Location = new Point(10, 60);
                warn.Visible = false;
                card.Controls.Add(warn);
                Action showStatus = delegate
                {
                    warn.Visible = s.Unresponsive;
                    card.Height = s.Unresponsive ? 98 : 62;
                    list.PerformLayout();
                    Height = list.PreferredSize.Height + 4;
                    ShowNearTrayIfVisible();
                };
                s.StatusChanged += delegate { if (IsHandleCreated) BeginInvoke(showStatus); };
                slider.ValueChanged += delegate
                {
                    valueLabel.Text = slider.Value + "%";
                    s.Set(slider.Value);
                    if (!linking && allSlider != null) { allSlider.SetSilently(AverageOfSliders()); allValue.Text = allSlider.Value + "%"; }
                };
            }
        }

        FlatSlider allSlider;
        Label allValue;

        int AverageOfSliders()
        {
            if (sliders.Count == 0) return 0;
            int sum = 0;
            foreach (var s in sliders) sum += s.Value;
            return sum / sliders.Count;
        }

        public void SetAll(int v)
        {
            if (allSlider != null) allSlider.Value = v;
            else foreach (var s in sliders) s.Value = v;
        }

        void ShowNearTrayIfVisible()
        {
            if (!Visible) return;
            var wa = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - 12, wa.Bottom - Height - 12);
        }

        public void ShowNearTray()
        {
            var wa = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - 12, wa.Bottom - Height - 12);
            Show();
            Activate();
        }
    }

    class App : ApplicationContext
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "BrightnessControl";

        readonly NotifyIcon tray;
        readonly Panel panel;
        readonly ToolStripMenuItem startup;
        List<Screen2> screens = new List<Screen2>();

        public App()
        {
            panel = new Panel();

            var menu = new ContextMenuStrip();
            menu.Items.Add("Open", null, delegate { Toggle(); });
            menu.Items.Add("Refresh screens", null, delegate { RefreshScreens(); });
            startup = new ToolStripMenuItem("Start with Windows", null, delegate { ToggleStartup(); });
            startup.Checked = IsStartupEnabled();
            menu.Items.Add(startup);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { ExitThread(); });

            tray = new NotifyIcon
            {
                Icon = MakeIcon(),
                Text = "Brightness Control",
                Visible = true,
                ContextMenuStrip = menu
            };
            tray.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) Toggle(); };

            IntPtr forceHandle = panel.Handle; // create the window handle so BeginInvoke works before first show
            SystemEvents.DisplaySettingsChanged += delegate { panel.BeginInvoke((MethodInvoker)RefreshScreens); };
            RefreshScreens();
        }

        void RefreshScreens()
        {
            Monitors.Release(screens);
            screens = Monitors.Detect();
            panel.Build(screens);
            tray.Text = "Brightness Control - " + screens.Count + " screen" + (screens.Count == 1 ? "" : "s");
        }

        void Toggle()
        {
            if (panel.Visible) { panel.Hide(); return; }
            // Clicking the tray icon while open hides the panel (via Deactivate) just before this runs;
            // treat that click as "close" rather than reopening.
            if ((DateTime.Now - panel.LastHidden).TotalMilliseconds < 300) return;
            panel.ShowNearTray();
        }

        static bool IsStartupEnabled()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue(RunName) != null;
        }

        void ToggleStartup()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (IsStartupEnabled()) k.DeleteValue(RunName, false);
                else k.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
            }
            startup.Checked = IsStartupEnabled();
        }

        static Icon MakeIcon()
        {
            using (var bmp = new Bitmap(32, 32))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (var pen = new Pen(Color.White, 2.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            double a = i * Math.PI / 4;
                            float x1 = 16 + (float)Math.Cos(a) * 10.5f, y1 = 16 + (float)Math.Sin(a) * 10.5f;
                            float x2 = 16 + (float)Math.Cos(a) * 14.5f, y2 = 16 + (float)Math.Sin(a) * 14.5f;
                            g.DrawLine(pen, x1, y1, x2, y2);
                        }
                        g.DrawEllipse(pen, 9.5f, 9.5f, 13, 13);
                    }
                    using (var b = new SolidBrush(Color.White)) g.FillPie(b, 9.5f, 9.5f, 13, 13, 90, 180);
                }
                IntPtr h = bmp.GetHicon();
                var icon = (Icon)Icon.FromHandle(h).Clone();
                Native.DestroyIcon(h);
                return icon;
            }
        }

        protected override void ExitThreadCore()
        {
            tray.Visible = false;
            tray.Dispose();
            Monitors.Release(screens);
            base.ExitThreadCore();
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, "BrightnessControl.SingleInstance", out created))
            {
                if (!created) return;
                ApplicationConfiguration.Initialize();
                Application.Run(new App());
            }
        }
    }
}
