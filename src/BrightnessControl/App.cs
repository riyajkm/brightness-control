using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BrightnessControl
{
    class App : ApplicationContext
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "BrightnessControl";

        readonly NotifyIcon tray;
        readonly Panel panel;
        readonly ToolStripMenuItem startup;
        readonly System.Windows.Forms.Timer refreshDelay = new System.Windows.Forms.Timer();
        List<Screen2> screens = new List<Screen2>();
        bool detecting, detectAgain;

        public App()
        {
            panel = new Panel();

            var menu = new ContextMenuStrip();
            menu.Items.Add("Open", null, delegate { Toggle(); });
            menu.Items.Add("Refresh screens", null, delegate { RefreshScreens(); });
            startup = new ToolStripMenuItem("Start with Windows", null, delegate { ToggleStartup(); });
            startup.Checked = IsStartupEnabled();
            menu.Items.Add(startup);
            menu.Items.Add("Open detection log", null, delegate { OpenLog(); });
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
            refreshDelay.Tick += delegate { refreshDelay.Stop(); RefreshScreens(); };
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            RefreshScreens();
        }

        // Display changes arrive in bursts (plug-in, resolution, sleep); detect once things settle.
        void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            panel.BeginInvoke((MethodInvoker)delegate { ScheduleRefresh(800); });
        }

        // Monitors take a few seconds to answer DDC/CI after the PC wakes up.
        void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume) panel.BeginInvoke((MethodInvoker)delegate { ScheduleRefresh(4000); });
        }

        void ScheduleRefresh(int ms)
        {
            refreshDelay.Stop();
            refreshDelay.Interval = ms;
            refreshDelay.Start();
        }

        // Detection does slow DDC/CI reads, so it runs on the thread pool. Old handles are released first
        // so the old and new handles never talk to the same monitor at once.
        void RefreshScreens()
        {
            if (detecting) { detectAgain = true; return; }
            detecting = true;
            Monitors.Release(screens);
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<Screen2> found;
                try { found = Monitors.Detect(); }
                catch (Exception e) { Log.Write("Detection failed: " + e); found = new List<Screen2>(); }
                panel.BeginInvoke((MethodInvoker)delegate
                {
                    screens = found;
                    panel.Build(screens);
                    tray.Text = "Brightness Control - " + screens.Count + " screen" + (screens.Count == 1 ? "" : "s");
                    detecting = false;
                    if (detectAgain) { detectAgain = false; RefreshScreens(); }
                });
            });
        }

        void Toggle()
        {
            if (panel.Visible) { panel.Hide(); return; }
            // Clicking the tray icon while open hides the panel (via Deactivate) just before this runs;
            // treat that click as "close" rather than reopening.
            if ((DateTime.Now - panel.LastHidden).TotalMilliseconds < 300) return;
            // Re-read every screen: picks up monitors that weren't answering and brightness changed elsewhere.
            if (!detecting) foreach (var s in screens) s.Refresh();
            panel.ShowNearTray();
        }

        static void OpenLog()
        {
            if (!File.Exists(Log.FilePath)) Log.Write("Log created");
            try { Process.Start(new ProcessStartInfo(Log.FilePath) { UseShellExecute = true }); }
            catch (Exception e) { MessageBox.Show("Couldn't open " + Log.FilePath + "\n\n" + e.Message, "Brightness Control"); }
        }

        static bool IsStartupEnabled()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue(RunName) != null;
        }

        void ToggleStartup()
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
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
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            tray.Visible = false;
            tray.Dispose();
            Monitors.Release(screens);
            base.ExitThreadCore();
        }
    }
}
