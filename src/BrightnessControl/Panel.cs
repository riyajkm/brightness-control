using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace BrightnessControl
{
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
            int sum = 0, n = 0;
            foreach (var s in screens) if (s.Controllable && s.Responding) { sum += s.Brightness; n++; }
            return n == 0 ? 50 : sum / n;
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
                    for (int i = 0; i < sliders.Count; i++) if (sliders[i].Enabled) sliders[i].Value = slider.Value;
                    linking = false;
                };
                allSlider = slider; allValue = valueLabel;
            }
            else
            {
                sliders.Add(slider);
                values.Add(valueLabel);
                var s = screens[index];

                // Status line shown when the screen can't be controlled, isn't answering, or ignores changes.
                var warn = MakeLabel("", Color.FromArgb(255, 185, 90), 8.5f, false);
                warn.AutoSize = false;
                warn.Size = new Size(width - 20, 34);
                warn.Location = new Point(10, 60);
                warn.Visible = false;
                card.Controls.Add(warn);
                Action showStatus = delegate
                {
                    string msg = null;
                    if (!s.Controllable) msg = "Windows doesn't offer brightness control for this screen.";
                    else if (!s.Responding) msg = "Not answering yet. It's re-checked each time you open this panel. Check DDC/CI is on in its menu.";
                    else if (s.Unresponsive) msg = "Monitor isn't accepting changes. In its own menu, turn DDC/CI on and set ECO Mode to Standard.";
                    slider.Enabled = s.Controllable;
                    if (s.Responding && !slider.Dragging && !s.WritePending && slider.Value != s.Brightness)
                    {
                        slider.SetSilently(s.Brightness);
                        if (allSlider != null) { allSlider.SetSilently(AverageOfSliders()); allValue.Text = allSlider.Value + "%"; }
                    }
                    valueLabel.Text = s.Responding ? slider.Value + "%" : "—";
                    warn.Text = msg ?? "";
                    warn.Visible = msg != null;
                    int h = msg != null ? 98 : 62;
                    if (card.Height == h) return;
                    card.Height = h;
                    list.PerformLayout();
                    Height = list.PreferredSize.Height + 4;
                    ShowNearTrayIfVisible();
                };
                showStatus();
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
            int sum = 0, n = 0;
            foreach (var s in sliders) if (s.Enabled) { sum += s.Value; n++; }
            return n == 0 ? 0 : sum / n;
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
}
