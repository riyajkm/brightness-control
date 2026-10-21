using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace BrightnessControl
{
    // "Match colours" wizard. A white (or grey) square appears on every screen; the user picks the screen whose
    // white looks right, then moves each other screen's Cooler-Warmer and Greener-Magenta sliders until its square
    // looks the same. Changes apply live; Cancel puts everything back.
    class ColorWizard : Form
    {
        readonly List<Screen2> screens = new List<Screen2>();
        readonly Dictionary<Screen2, int[]> before = new Dictionary<Screen2, int[]>();   // warmth, tint at open
        readonly Dictionary<Screen2, int[]> current = new Dictionary<Screen2, int[]>();
        readonly List<PatchForm> patches = new List<PatchForm>();
        readonly ComboBox referenceBox;
        readonly CheckBox greyBox;
        readonly Label status;
        readonly System.Windows.Forms.Panel rowsPanel;
        readonly Button save, cancel, reset;
        Screen2 reference;
        bool saved;
        int preparing;

        public static bool CanRun(List<Screen2> all)
        {
            int n = 0;
            foreach (var s in all) if (ColorBalance.CanAdjust(s)) n++;
            return n >= 2;
        }

        public ColorWizard(List<Screen2> all)
        {
            foreach (var s in all)
                if (ColorBalance.CanAdjust(s))
                {
                    screens.Add(s);
                    var m = Settings.Current.For(s.Id);
                    before[s] = new[] { m.Warmth, m.Tint };
                    current[s] = new[] { m.Warmth, m.Tint };
                }
            reference = screens.Find(s => s.Kind == "WMI") ?? screens[0];

            Text = "Match colours";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = true;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);

            Controls.Add(new Label
            {
                Text = "Pick the screen whose white looks right to you. Then move each other screen's sliders until its square looks the same. Tip: match brightness first.",
                Location = new Point(16, 12), Size = new Size(468, 40), ForeColor = Theme.Text
            });
            Controls.Add(new Label { Text = "Reference screen", Location = new Point(16, 62), AutoSize = true, ForeColor = Theme.Muted });
            referenceBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(140, 58), Width = 220, AccessibleName = "Reference screen" };
            foreach (var s in screens) referenceBox.Items.Add(s.Name);
            referenceBox.SelectedIndex = screens.IndexOf(reference);
            referenceBox.SelectedIndexChanged += delegate { reference = screens[referenceBox.SelectedIndex]; BuildRows(); RelabelPatches(); };
            Controls.Add(referenceBox);
            greyBox = new CheckBox { Text = "Grey squares", Location = new Point(376, 60), AutoSize = true, ForeColor = Theme.Text };
            greyBox.CheckedChanged += delegate { foreach (var p in patches) p.BackColor = greyBox.Checked ? Color.FromArgb(128, 128, 128) : Color.White; };
            Controls.Add(greyBox);

            rowsPanel = new System.Windows.Forms.Panel { Location = new Point(0, 92), Width = 500, BackColor = Theme.Back };
            Controls.Add(rowsPanel);
            status = new Label { Location = new Point(16, 0), Size = new Size(180, 34), ForeColor = Theme.Muted };
            Controls.Add(status);
            reset = MakeButton("Reset all");
            cancel = MakeButton("Cancel");
            save = MakeButton("Save");
            reset.Click += delegate { ResetAll(); };
            cancel.Click += delegate { Close(); };
            save.Click += delegate { Save(); };
            AcceptButton = save;
            CancelButton = cancel;
            BuildRows();
        }

        Button MakeButton(string text)
        {
            var b = new Button { Text = text, Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, BackColor = Theme.Card, ForeColor = Theme.Text };
            b.FlatAppearance.BorderColor = Theme.Border;
            Controls.Add(b);
            return b;
        }

        // One block of two sliders per screen except the reference.
        void BuildRows()
        {
            foreach (Control c in rowsPanel.Controls) c.Dispose();
            rowsPanel.Controls.Clear();
            int y = 0;
            foreach (var s in screens)
            {
                if (s == reference) continue;
                var screen = s;
                rowsPanel.Controls.Add(new Label { Text = s.Name, Location = new Point(16, y), AutoSize = true, ForeColor = Theme.Text, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) });
                rowsPanel.Controls.Add(new Label { Text = ColorBalance.Describe(s), Location = new Point(16, y + 20), AutoSize = true, ForeColor = Theme.Muted });
                var warm = AddSlider(y + 40, "Cooler", "Warmer", s.Name + " warmth", current[s][0]);
                var tint = AddSlider(y + 78, "Greener", "Magenta", s.Name + " tint", current[s][1]);
                EventHandler changed = delegate
                {
                    current[screen] = new[] { warm.Value - 50, tint.Value - 50 };
                    ColorBalance.Apply(screen, current[screen][0], current[screen][1]);
                };
                warm.ValueChanged += changed;
                tint.ValueChanged += changed;
                warm.Enabled = tint.Enabled = preparing == 0;
                y += 124;
            }
            rowsPanel.Height = Math.Max(y, 10);
            int bottom = rowsPanel.Bottom + 8;
            status.Top = bottom + 4;
            reset.Location = new Point(204, bottom);
            cancel.Location = new Point(300, bottom);
            save.Location = new Point(396, bottom);
            ClientSize = new Size(500, bottom + 44);
            if (IsHandleCreated) PlaceWindow();
        }

        FlatSlider AddSlider(int y, string left, string right, string name, int value)
        {
            rowsPanel.Controls.Add(new Label { Text = left, Location = new Point(16, y + 6), Size = new Size(64, 20), ForeColor = Theme.Muted });
            var slider = new FlatSlider { Location = new Point(80, y), Size = new Size(330, 28), AccessibleName = name };
            slider.SetSilently(value + 50);
            rowsPanel.Controls.Add(slider);
            rowsPanel.Controls.Add(new Label { Text = right, Location = new Point(416, y + 6), Size = new Size(70, 20), ForeColor = Theme.Muted });
            return slider;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            PlaceWindow();
            foreach (var s in screens)
            {
                if (s.Bounds.IsEmpty) continue;
                var p = new PatchForm(s.Name, Color.White);
                p.PlaceOn(s.Bounds);
                p.Show();
                p.Tag = s;
                patches.Add(p);
            }
            RelabelPatches();

            // Monitors adjusted in hardware get their current settings saved (and checked) before the first change.
            foreach (var s in screens) if (ColorBalance.UsesGains(s)) preparing++;
            if (preparing > 0)
            {
                status.Text = "Getting monitors ready…";
                foreach (var s in screens)
                    if (ColorBalance.UsesGains(s))
                        ColorBalance.Prepare(s, delegate { if (IsHandleCreated) BeginInvoke((MethodInvoker)OnePrepared); });
            }
            ApplyAll();
        }

        void OnePrepared()
        {
            if (--preparing > 0) return;
            status.Text = "";
            BuildRows();   // a monitor may have switched to Windows colour output if it ignored hardware changes
            ApplyAll();
        }

        void ApplyAll()
        {
            foreach (var s in screens) if (preparing == 0 || !ColorBalance.UsesGains(s)) ColorBalance.Apply(s, current[s][0], current[s][1]);
        }

        void RelabelPatches()
        {
            foreach (var p in patches)
            {
                var s = (Screen2)p.Tag;
                p.Controls[0].Text = s == reference ? s.Name + " (reference)" : s.Name;
            }
        }

        void PlaceWindow()
        {
            var area = reference.Bounds.IsEmpty ? System.Windows.Forms.Screen.PrimaryScreen.WorkingArea : System.Windows.Forms.Screen.FromRectangle(reference.Bounds).WorkingArea;
            Location = new Point(area.Left + (area.Width - Width) / 2, area.Bottom - Height - 24);
        }

        void ResetAll()
        {
            foreach (var s in screens)
            {
                ColorBalance.Reset(s);
                current[s] = new[] { 0, 0 };
            }
            BuildRows();
        }

        void Save()
        {
            foreach (var s in screens)
            {
                var m = Settings.Current.For(s.Id);
                m.Warmth = current[s][0];
                m.Tint = current[s][1];
                if (m.Warmth != 0 || m.Tint != 0) Log.Write("Colour matched " + s.Name + ": warmth " + m.Warmth + ", tint " + m.Tint + " - " + ColorBalance.Describe(s));
            }
            Settings.Current.Save();
            saved = true;
            Close();
        }

        // Cancelled: put every screen back as it was when the wizard opened.
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            foreach (var p in patches) p.Close();
            if (!saved)
                foreach (var s in screens)
                {
                    var b = before[s];
                    if (b[0] == 0 && b[1] == 0) ColorBalance.Reset(s);
                    else ColorBalance.Apply(s, b[0], b[1]);
                }
            base.OnFormClosed(e);
        }
    }
}
