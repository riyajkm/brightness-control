using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BrightnessControl
{
    class App : ApplicationContext
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "BrightnessControl";
        const string StartupTaskId = "BrightnessControlStartup";   // must match Package.appxmanifest
        const string RepoUrl = "https://github.com/riyajkm/brightness-control";

        readonly NotifyIcon tray;
        readonly Panel panel;
        readonly ContextMenuStrip menu;
        readonly ToolStripMenuItem startup, match, calibrate, follow, matchColours, resetColours;
        readonly System.Windows.Forms.Timer refreshDelay = new System.Windows.Forms.Timer();
        List<Screen2> screens = new List<Screen2>();
        bool detecting, detectAgain, wizardOpen;   // wizardOpen covers both match wizards

        public App()
        {
            panel = new Panel();

            menu = new ContextMenuStrip();
            menu.Items.Add("Open", null, delegate { ShowPanel(Cursor.Position); });
            menu.Items.Add("Refresh screens", null, delegate { RefreshScreens(); });
            menu.Items.Add(new ToolStripSeparator());
            match = new ToolStripMenuItem("Match brightness across screens", null, delegate { ToggleMatch(); });
            calibrate = new ToolStripMenuItem("Calibrate matching…", null, delegate { RunWizard(); });
            follow = new ToolStripMenuItem("Follow laptop brightness", null, delegate { ToggleFollow(); });
            matchColours = new ToolStripMenuItem("Match colours…", null, delegate { RunColorWizard(); });
            resetColours = new ToolStripMenuItem("Reset colours", null, delegate { ResetColours(); });
            menu.Items.Add(match);
            menu.Items.Add(calibrate);
            menu.Items.Add(follow);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(matchColours);
            menu.Items.Add(resetColours);
            menu.Items.Add(new ToolStripSeparator());
            startup = new ToolStripMenuItem("Start with Windows", null, async delegate { await ToggleStartup(); });
            menu.Items.Add(startup);
            menu.Items.Add("Open detection log", null, delegate { OpenLog(); });
            menu.Items.Add("About", null, delegate { ShowAbout(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { ExitThread(); });
            menu.Opening += async delegate { UpdateMenu(); await UpdateStartupCheck(); };
            panel.MoreMenu = menu;

            tray = new NotifyIcon
            {
                Icon = MakeIcon(),
                Text = "Brightness Control",
                Visible = true,
                ContextMenuStrip = menu
            };
            tray.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) Toggle(); };

            IntPtr forceHandle = panel.Handle; // create the window handle so BeginInvoke works before first show
            panel.Rebuild();
            refreshDelay.Tick += delegate { refreshDelay.Stop(); RefreshScreens(); };
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            LaptopWatcher.Changed += OnLaptopChanged;
            if (Settings.Current.FollowLaptop) LaptopWatcher.Start();
            RefreshScreens();
        }

        // Called (on any thread) when someone starts the app while it's already running.
        public void ShowFromOtherInstance()
        {
            if (panel.IsHandleCreated) panel.BeginInvoke((MethodInvoker)delegate { ShowPanel(TrayCorner()); });
        }

        static Point TrayCorner()
        {
            var b = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            return new Point(b.Right - 1, b.Bottom - 1);
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

        // Light/dark theme switched in Windows settings.
        void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General) return;
            panel.BeginInvoke((MethodInvoker)delegate
            {
                if (!Theme.Refresh()) return;
                panel.Rebuild();
                var old = tray.Icon;
                tray.Icon = MakeIcon();
                old.Dispose();
            });
        }

        void OnLaptopChanged(int value)
        {
            if (!Settings.Current.FollowLaptop) return;
            panel.BeginInvoke((MethodInvoker)delegate { panel.FollowLaptop(value); });
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
            if (detecting || wizardOpen) { detectAgain = true; return; }
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
                    foreach (var s in screens) s.LoadCapabilities();
                    ColorBalance.ReapplySoftware(screens);   // gamma ramps don't survive display changes
                    panel.Build(screens);
                    tray.Text = "Brightness Control - " + screens.Count + " screen" + (screens.Count == 1 ? "" : "s");
                    detecting = false;
                    if (detectAgain) { detectAgain = false; RefreshScreens(); }
                    else if (!Settings.Current.FirstRunDone) FirstRun();
                });
            });
        }

        // Show the panel once so people can see where the app went, and point at the tray icon.
        void FirstRun()
        {
            Settings.Current.FirstRunDone = true;
            Settings.Current.Save();
            ShowPanel(TrayCorner());
            tray.ShowBalloonTip(8000, "Brightness Control is running",
                "Click the sun icon in the taskbar to change brightness. Don't see it? Click ^ next to the clock and drag the sun onto the taskbar.",
                ToolTipIcon.Info);
        }

        void Toggle()
        {
            if (panel.Visible) { panel.Hide(); return; }
            // Clicking the tray icon while open hides the panel (via Deactivate) just before this runs;
            // treat that click as "close" rather than reopening.
            if ((DateTime.Now - panel.LastHidden).TotalMilliseconds < 300) return;
            ShowPanel(Cursor.Position);
        }

        void ShowPanel(Point anchor)
        {
            // Re-read every screen: picks up monitors that weren't answering and brightness changed elsewhere.
            if (!detecting)
            {
                foreach (var s in screens) s.Refresh();
                ColorBalance.ReapplySoftware(screens);   // in case other software (or Night Light) replaced it
            }
            panel.ShowAt(anchor);
        }

        void UpdateMenu()
        {
            bool hasLaptop = false;
            int controllable = 0;
            foreach (var s in screens)
            {
                if (s.Kind == "WMI") hasLaptop = true;
                if (s.Controllable) controllable++;
            }
            match.Checked = Settings.Current.MatchBrightness;
            match.Enabled = controllable > 1;
            calibrate.Enabled = MatchWizard.CanRun(screens) && !wizardOpen;
            follow.Checked = Settings.Current.FollowLaptop;
            follow.Enabled = hasLaptop && controllable > 1;
            matchColours.Enabled = ColorWizard.CanRun(screens) && !wizardOpen;
            bool anyColour = false;
            foreach (var s in screens) { var m = Settings.Current.For(s.Id); if (m.Warmth != 0 || m.Tint != 0) anyColour = true; }
            resetColours.Enabled = anyColour && !wizardOpen;
        }

        void ToggleMatch()
        {
            Settings.Current.MatchBrightness = !Settings.Current.MatchBrightness;
            Settings.Current.Save();
            panel.Rebuild();
        }

        void ToggleFollow()
        {
            Settings.Current.FollowLaptop = !Settings.Current.FollowLaptop;
            Settings.Current.Save();
            if (Settings.Current.FollowLaptop) LaptopWatcher.Start();
            else LaptopWatcher.Stop();
        }

        void RunWizard()
        {
            if (wizardOpen || !MatchWizard.CanRun(screens)) return;
            panel.Hide();
            wizardOpen = true;
            using (var w = new MatchWizard(screens)) w.ShowDialog();
            wizardOpen = false;
            panel.Rebuild();
            if (detectAgain) { detectAgain = false; RefreshScreens(); }
        }

        // Notepad first: the .log file association is sometimes broken (opening it silently does nothing).
        // Then the association, then File Explorer with the file selected.
        void RunColorWizard()
        {
            if (wizardOpen || !ColorWizard.CanRun(screens)) return;
            panel.Hide();
            wizardOpen = true;
            using (var w = new ColorWizard(screens)) w.ShowDialog();
            wizardOpen = false;
            if (detectAgain) { detectAgain = false; RefreshScreens(); }
        }

        void ResetColours()
        {
            foreach (var s in screens) ColorBalance.Reset(s);
        }

        void OpenLog()
        {
            if (!File.Exists(Log.FilePath)) Log.Write("Log created");
            string quoted = "\"" + Log.FilePath + "\"";
            var attempts = new[]
            {
                new ProcessStartInfo("notepad.exe", quoted) { UseShellExecute = true },
                new ProcessStartInfo(Log.FilePath) { UseShellExecute = true },
                new ProcessStartInfo("explorer.exe", "/select," + quoted) { UseShellExecute = true }
            };
            Exception last = null;
            foreach (var start in attempts)
            {
                try { Process.Start(start); return; }
                catch (Exception e) { last = e; }
            }
            MessageBox.Show("Couldn't open " + Log.FilePath + "\n\n" + last.Message, "Brightness Control");
        }

        static void ShowAbout()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var answer = MessageBox.Show(
                "Brightness Control " + version.ToString(3) + "\n\n" +
                "Controls brightness on every screen: external monitors over DDC/CI, laptop screens through Windows.\n" +
                "Collects no data and needs no internet connection.\n\n" +
                "Open the project page?",
                "About Brightness Control", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (answer == DialogResult.Yes)
                try { Process.Start(new ProcessStartInfo(RepoUrl) { UseShellExecute = true }); } catch { }
        }

        // Packaged (Store) builds use the manifest's StartupTask, which shows up in Settings > Apps > Startup.
        // The plain exe uses the classic Run key.
        async Task UpdateStartupCheck()
        {
            try
            {
                if (AppInfo.IsPackaged)
                {
                    var task = await Windows.ApplicationModel.StartupTask.GetAsync(StartupTaskId);
                    startup.Checked = task.State == Windows.ApplicationModel.StartupTaskState.Enabled || task.State == Windows.ApplicationModel.StartupTaskState.EnabledByPolicy;
                }
                else
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) startup.Checked = k != null && k.GetValue(RunName) != null;
                }
            }
            catch (Exception e) { Log.Write("Startup state unavailable: " + e.Message); }
        }

        async Task ToggleStartup()
        {
            try
            {
                if (AppInfo.IsPackaged)
                {
                    var task = await Windows.ApplicationModel.StartupTask.GetAsync(StartupTaskId);
                    if (task.State == Windows.ApplicationModel.StartupTaskState.Enabled) task.Disable();
                    else
                    {
                        var state = await task.RequestEnableAsync();
                        if (state == Windows.ApplicationModel.StartupTaskState.DisabledByUser)
                            MessageBox.Show("Starting with Windows was turned off in Settings. Turn it back on under Settings > Apps > Startup.", "Brightness Control");
                    }
                }
                else
                {
                    using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                    {
                        if (k.GetValue(RunName) != null) k.DeleteValue(RunName, false);
                        else k.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
                    }
                }
            }
            catch (Exception e) { Log.Write("Couldn't change startup: " + e.Message); }
            await UpdateStartupCheck();
        }

        // Sun icon, white on a dark taskbar and near-black on a light one.
        static Icon MakeIcon()
        {
            var color = Theme.TaskbarDark ? Color.White : Color.FromArgb(28, 28, 28);
            using (var bmp = new Bitmap(32, 32))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (var pen = new Pen(color, 2.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
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
                    using (var b = new SolidBrush(color)) g.FillPie(b, 9.5f, 9.5f, 13, 13, 90, 180);
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
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            LaptopWatcher.Changed -= OnLaptopChanged;
            LaptopWatcher.Stop();
            tray.Visible = false;
            tray.Dispose();
            Monitors.Release(screens);
            base.ExitThreadCore();
        }
    }
}
