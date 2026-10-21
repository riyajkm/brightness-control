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

namespace DisplayControl
{
    class App : ApplicationContext
    {
        const string RunKey = Lifecycle.RunKey;
        const string RunName = Lifecycle.RunName;
        const string OldRunName = "BrightnessControl";   // the app's name before 1.0.1
        const string StartupTaskId = "DisplayControlStartup";   // must match Package.appxmanifest
        const string RepoUrl = "https://github.com/riyajkm/display-control";

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
                Text = "Display Control",
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
            MigrateStartup();
            if (Settings.Current.FollowLaptop) LaptopWatcher.Start();
            RefreshScreens();
        }

        // An unexpected error was caught and logged. Say so once per run; the app keeps working.
        bool errorShown;
        public void NotifyError()
        {
            if (errorShown || !panel.IsHandleCreated) return;
            errorShown = true;
            panel.BeginInvoke((MethodInvoker)delegate
            {
                tray.ShowBalloonTip(6000, "Display Control hit a problem",
                    "It's still running. Details are in the detection log (right-click the tray icon > Open detection log).", ToolTipIcon.Warning);
            });
        }

        // "DisplayControl.exe --exit" (the installer, before updating): close cleanly, which also removes
        // software colour adjustments. Called on any thread.
        public void ExitFromSignal()
        {
            if (panel.IsHandleCreated) panel.BeginInvoke((MethodInvoker)delegate { Log.Write("Closing on request"); ExitThread(); });
        }

        // "DisplayControl.exe --uninstall": put matched colours back on every monitor, remove the startup
        // entry, then close once the monitors have finished restoring (at most ~20 s). Called on any thread.
        public void UninstallFromSignal()
        {
            if (!panel.IsHandleCreated) return;
            panel.BeginInvoke((MethodInvoker)delegate
            {
                panel.Hide();
                Lifecycle.RemoveStartup();
                Lifecycle.ResetAllColours(screens);
                var started = DateTime.Now;
                var wait = new System.Windows.Forms.Timer { Interval = 250 };
                wait.Tick += delegate
                {
                    if (!Lifecycle.AllIdle(screens) && (DateTime.Now - started).TotalSeconds < 20) return;
                    wait.Stop();
                    ExitThread();
                };
                wait.Start();
            });
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
                    tray.Text = "Display Control - " + screens.Count + " screen" + (screens.Count == 1 ? "" : "s");
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
            tray.ShowBalloonTip(8000, "Display Control is running",
                "Click the monitor icon in the taskbar to control your screens. Don't see it? Click ^ next to the clock and drag it onto the taskbar.",
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

        void RunColorWizard()
        {
            if (wizardOpen || !ColorWizard.CanRun(screens)) return;
            panel.Hide();
            wizardOpen = true;
            using (var w = new ColorWizard(screens)) w.ShowDialog();
            wizardOpen = false;
            // Re-detecting releases the monitors, which drops queued work - give colour restores time to finish.
            if (detectAgain) { detectAgain = false; ScheduleRefresh(8000); }
        }

        void ResetColours()
        {
            foreach (var s in screens) ColorBalance.Reset(s);
        }

        // Notepad first: the .log file association is sometimes broken (opening it silently does nothing).
        // Then the association, then File Explorer with the file selected.
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
            MessageBox.Show("Couldn't open " + Log.FilePath + "\n\n" + last.Message, "Display Control");
        }

        static void ShowAbout()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var answer = MessageBox.Show(
                "Display Control " + version.ToString(3) + "\n\n" +
                "Brightness, colour and settings for every screen: external monitors over DDC/CI, laptop screens through Windows.\n" +
                "Collects no data and needs no internet connection.\n\n" +
                "Open the project page?",
                "About Display Control", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (answer == DialogResult.Yes)
                try { Process.Start(new ProcessStartInfo(RepoUrl) { UseShellExecute = true }); } catch { }
        }

        // Someone who had "Start with Windows" on under the old name keeps it, pointing at this exe.
        static void MigrateStartup()
        {
            if (AppInfo.IsPackaged) return;
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null || k.GetValue(OldRunName) == null) return;
                    k.DeleteValue(OldRunName, false);
                    k.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
                    Log.Write("Start with Windows moved to the new name");
                }
            }
            catch (Exception e) { Log.Write("Couldn't move Start with Windows: " + e.Message); }
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
                            MessageBox.Show("Starting with Windows was turned off in Settings. Turn it back on under Settings > Apps > Startup.", "Display Control");
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
                    using (var pen = new Pen(color, 2.6f) { LineJoin = LineJoin.Round })
                    using (var b = new SolidBrush(color))
                    {
                        // Monitor outline with a stand, and a small sun on the screen.
                        g.DrawRectangle(pen, 2.3f, 3.3f, 27.4f, 18.4f);
                        g.FillRectangle(b, 14.5f, 21.5f, 3f, 4.5f);
                        g.FillRectangle(b, 9f, 26f, 14f, 2.6f);
                        g.FillEllipse(b, 12f, 8.5f, 8f, 8f);
                        using (var ray = new Pen(color, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                            for (int i = 0; i < 8; i++)
                            {
                                double a = i * Math.PI / 4;
                                g.DrawLine(ray, 16 + (float)Math.Cos(a) * 6f, 12.5f + (float)Math.Sin(a) * 6f,
                                                16 + (float)Math.Cos(a) * 7.2f, 12.5f + (float)Math.Sin(a) * 7.2f);
                            }
                    }
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
            ColorBalance.RemoveSoftware(screens);
            tray.Visible = false;
            tray.Dispose();
            Monitors.Release(screens);
            base.ExitThreadCore();
        }
    }
}
