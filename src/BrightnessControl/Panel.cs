using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace BrightnessControl
{
    // The flyout opened from the tray icon. All sizes go through S() so it scales with the screen's DPI.
    class Panel : Form
    {
        readonly FlowLayoutPanel list;
        readonly List<Row> rows = new List<Row>();
        readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        readonly HashSet<string> expanded = new HashSet<string>();
        readonly List<KeyValuePair<Screen2, Action<Screen2>>> subscriptions = new List<KeyValuePair<Screen2, Action<Screen2>>>();
        List<Screen2> screens = new List<Screen2>();
        bool searching = true;
        FlatSlider allSlider;
        Label allValue;
        bool linking;
        Point anchor;

        public DateTime LastHidden = DateTime.MinValue;
        public ContextMenuStrip MoreMenu;  // the tray menu, also opened from the panel's "more" button

        class Row
        {
            public Screen2 Screen;
            public FlatSlider Slider;
            public Action Update;
        }

        public Panel()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            Text = "Brightness";

            list = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = false
            };
            Controls.Add(list);
            Deactivate += delegate { LastHidden = DateTime.Now; Hide(); };
        }

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; /* drop shadow */ return cp; }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { Hide(); e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Theme.Border)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }

        // Moving to a screen with different scaling: rebuild at the new size.
        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            Rebuild();
            Position();
        }

        int S(int px) { return (int)Math.Round(px * DeviceDpi / 96.0); }

        Font F(float pt, bool bold = false)
        {
            string key = pt + (bold ? "b" : "") + "@" + DeviceDpi;
            Font f;
            if (!fonts.TryGetValue(key, out f))
            {
                f = new Font("Segoe UI", pt * DeviceDpi / 72f, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
                fonts[key] = f;
            }
            return f;
        }

        Font Icons(float pt)
        {
            string key = "icons" + pt + "@" + DeviceDpi;
            Font f;
            if (!fonts.TryGetValue(key, out f))
            {
                f = new Font("Segoe MDL2 Assets", pt * DeviceDpi / 72f, FontStyle.Regular, GraphicsUnit.Pixel);
                fonts[key] = f;
            }
            return f;
        }

        Label MakeLabel(string text, Color color, float size, bool bold)
        {
            return new Label { Text = text, ForeColor = color, Font = F(size, bold), AutoSize = true, Margin = new Padding(S(2), 0, 0, S(2)) };
        }

        Button MakeButton(string text, EventHandler onClick)
        {
            var b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                Font = F(9.5f),
                Height = S(30),
                Width = S(69),
                Margin = new Padding(0, 0, S(6), 0),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = Theme.Border;
            b.FlatAppearance.MouseOverBackColor = Theme.Hover;
            b.Click += onClick;
            return b;
        }

        Button MakeIconButton(string glyph, string name)
        {
            var b = new Button
            {
                Text = glyph,
                Font = Icons(9f),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Theme.Muted,
                Size = new Size(S(28), S(24)),
                Cursor = Cursors.Hand,
                AccessibleName = name,
                TabStop = true
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Theme.Hover;
            return b;
        }

        // Drawn by hand so the list follows the light/dark theme.
        ComboBox MakeCombo()
        {
            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                DrawMode = DrawMode.OwnerDrawFixed,
                BackColor = Theme.Back,
                ForeColor = Theme.Text,
                Font = F(9f),
                ItemHeight = S(20)
            };
            combo.DrawItem += delegate(object sender, DrawItemEventArgs e)
            {
                bool selected = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
                using (var b = new SolidBrush(selected ? Theme.Accent : Theme.Back)) e.Graphics.FillRectangle(b, e.Bounds);
                if (e.Index < 0) return;
                var color = selected ? (Theme.Dark ? Color.Black : Color.White) : Theme.Text;
                TextRenderer.DrawText(e.Graphics, combo.Items[e.Index].ToString(), combo.Font, e.Bounds, color, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };
            return combo;
        }

        public void Build(List<Screen2> found)
        {
            screens = found;
            searching = false;
            Rebuild();
        }

        // Recreate every control: after detection, a DPI change or a theme change.
        public void Rebuild()
        {
            BackColor = Theme.Back;
            Width = S(340);
            Padding = new Padding(1);
            list.BackColor = Theme.Back;
            list.Padding = new Padding(S(12), S(10), S(12), S(8));

            list.SuspendLayout();
            foreach (var sub in subscriptions) sub.Key.StatusChanged -= sub.Value;
            subscriptions.Clear();
            var old = new List<Control>();
            foreach (Control c in list.Controls) old.Add(c);
            list.Controls.Clear();
            foreach (var c in old) c.Dispose();
            rows.Clear();
            allSlider = null;
            allValue = null;
            int inner = Width - S(26);

            list.Controls.Add(MakeHeader(inner));

            if (searching || screens.Count == 0)
            {
                var none = MakeLabel(searching ? "Looking for screens…" : "No screens found.\nRight-click the tray icon and choose Refresh screens.", Theme.Muted, 9.5f, false);
                none.Margin = new Padding(S(2), 0, 0, S(12));
                list.Controls.Add(none);
            }

            int controllable = 0;
            foreach (var s in screens) if (s.Controllable) controllable++;
            if (controllable > 1) AddAllRow(inner);
            foreach (var s in screens) AddRow(s, inner);

            var presets = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0, S(6), 0, S(4)), BackColor = Theme.Back };
            presets.Controls.Add(MakeButton("Night", delegate { SetAll(20); }));
            presets.Controls.Add(MakeButton("Evening", delegate { SetAll(45); }));
            presets.Controls.Add(MakeButton("Day", delegate { SetAll(75); }));
            presets.Controls.Add(MakeButton("Max", delegate { SetAll(100); }));
            list.Controls.Add(presets);

            list.ResumeLayout();
            FitHeight();
            Invalidate();
        }

        Control MakeHeader(int width)
        {
            var header = new System.Windows.Forms.Panel { Width = width, Height = S(32), BackColor = Theme.Back, Margin = new Padding(0, 0, 0, S(6)) };
            var title = MakeLabel("Brightness", Theme.Text, 12f, true);
            title.Location = new Point(0, S(2));
            var more = MakeIconButton("", "More options");
            more.Location = new Point(width - more.Width, S(4));
            more.Click += delegate { if (MoreMenu != null) MoreMenu.Show(more, new Point(0, more.Height)); };
            header.Controls.Add(title);
            header.Controls.Add(more);
            return header;
        }

        void FitHeight()
        {
            list.PerformLayout();
            int h = list.PreferredSize.Height + 2;
            if (Height != h) { Height = h; if (Visible) Position(); }
        }

        System.Windows.Forms.Panel MakeCard(int width)
        {
            return new System.Windows.Forms.Panel { Width = width, Height = S(62), BackColor = Theme.Card, Margin = new Padding(0, 0, 0, S(8)) };
        }

        void AddAllRow(int width)
        {
            var card = MakeCard(width);
            var name = MakeLabel("All screens", Theme.Accent, 9.5f, true);
            name.Location = new Point(S(10), S(8));
            var value = MakeValueLabel(width);
            var slider = new FlatSlider { Width = width - S(8), Height = S(28), Location = new Point(S(4), S(30)), AccessibleName = "All screens brightness" };
            slider.SetSilently(AverageLevel());
            value.Text = slider.Value + "%";
            card.Controls.Add(name);
            card.Controls.Add(value);
            card.Controls.Add(slider);
            list.Controls.Add(card);

            // With matching on, this slider is a shared level and each screen gets its own % for it.
            slider.ValueChanged += delegate
            {
                value.Text = slider.Value + "%";
                if (linking) return;
                linking = true;
                foreach (var r in rows) if (r.Slider.Enabled) r.Slider.Value = Matching.ToPercent(Matching.CurveFor(r.Screen), slider.Value);
                linking = false;
            };
            allSlider = slider;
            allValue = value;
        }

        Label MakeValueLabel(int width)
        {
            var v = MakeLabel("", Theme.Muted, 9.5f, false);
            v.AutoSize = false;
            v.TextAlign = ContentAlignment.MiddleRight;
            v.Size = new Size(S(56), S(20));
            v.Location = new Point(width - S(66), S(8));
            return v;
        }

        void AddRow(Screen2 s, int width)
        {
            var card = MakeCard(width);
            var name = MakeLabel(s.Name, Theme.Text, 9.5f, false);
            name.Location = new Point(S(10), S(8));
            var value = MakeValueLabel(width);
            var more = MakeIconButton("", "Monitor settings for " + s.Name);
            more.Location = new Point(width - S(66) - more.Width, S(5));
            more.Visible = false;
            var slider = new FlatSlider { Width = width - S(8), Height = S(28), Location = new Point(S(4), S(30)), AccessibleName = s.Name + " brightness" };
            slider.SetSilently(s.Brightness);
            var warn = MakeLabel("", Theme.Warning, 8.5f, false);
            warn.AutoSize = false;
            warn.Size = new Size(width - S(20), S(34));
            warn.Visible = false;
            card.Controls.Add(name);
            card.Controls.Add(more);
            card.Controls.Add(value);
            card.Controls.Add(slider);
            card.Controls.Add(warn);
            list.Controls.Add(card);

            // Tracked in variables: Control.Visible reads false whenever the panel itself is hidden.
            Control extras = null;
            bool hasExtras = false, open = false, warnShown = false;
            var row = new Row { Screen = s, Slider = slider };
            rows.Add(row);

            Action relayout = delegate
            {
                int y = S(60);
                if (warnShown) { warn.Location = new Point(S(10), y); y += S(36); }
                if (open && extras != null) { extras.Location = new Point(0, y); y += extras.Height; }
                int h = y + S(2);
                if (card.Height == h) return;
                card.Height = h;
                FitHeight();
            };

            row.Update = delegate
            {
                string msg = null;
                if (!s.Controllable) msg = "Windows doesn't offer brightness control for this screen.";
                else if (!s.Responding) msg = "Not answering yet. It's re-checked each time you open this panel. Check DDC/CI is on in its menu.";
                else if (s.Unresponsive) msg = "Monitor isn't accepting changes. In its own menu, turn DDC/CI on and set ECO Mode to Standard.";
                slider.Enabled = s.Controllable;
                if (s.Responding && !slider.Dragging && !s.WritePending && slider.Value != s.Brightness)
                {
                    slider.SetSilently(s.Brightness);
                    UpdateAll();
                }
                value.Text = s.Responding ? slider.Value + "%" : "—";
                warn.Text = msg ?? "";
                warnShown = msg != null;
                warn.Visible = warnShown;
                var caps = s.Caps;
                hasExtras = s.Responding && caps != null && (caps.Has(Vcp.Contrast) || caps.Has(Vcp.ColorPreset) || caps.Has(Vcp.Input));
                more.Visible = hasExtras;
                if (!hasExtras && open) { open = false; extras.Visible = false; }
                relayout();
            };

            Action toggle = delegate
            {
                open = !open;
                if (open && extras == null) { extras = BuildExtras(s, width); card.Controls.Add(extras); }
                if (extras != null) extras.Visible = open;
                if (open) expanded.Add(s.Id); else expanded.Remove(s.Id);
                more.Text = open ? "" : "";
                more.AccessibleName = (open ? "Hide" : "Show") + " monitor settings for " + s.Name;
                relayout();
            };
            more.Click += delegate { toggle(); };

            row.Update();
            if (expanded.Contains(s.Id) && hasExtras) toggle();
            Action<Screen2> onStatus = delegate
            {
                if (IsHandleCreated && !IsDisposed) BeginInvoke((MethodInvoker)delegate { if (!slider.IsDisposed) row.Update(); });
            };
            s.StatusChanged += onStatus;
            subscriptions.Add(new KeyValuePair<Screen2, Action<Screen2>>(s, onStatus));
            slider.ValueChanged += delegate
            {
                value.Text = slider.Value + "%";
                s.Set(slider.Value);
                if (!linking) UpdateAll();
            };
        }

        // Contrast, colour preset and input source - only the ones this monitor lists in its capabilities.
        Control BuildExtras(Screen2 s, int width)
        {
            var caps = s.Caps;
            var box = new System.Windows.Forms.Panel { Width = width, BackColor = Theme.Card };
            int y = 0;
            int labelW = S(74), x = S(10) + labelW, w = width - x - S(10);
            FlatSlider contrast = null;
            ComboBox preset = null, input = null;

            Func<string, Label> label = delegate(string text)
            {
                var l = MakeLabel(text, Theme.Muted, 9f, false);
                l.Location = new Point(S(10), y + S(5));
                box.Controls.Add(l);
                return l;
            };

            if (caps.Has(Vcp.Contrast))
            {
                label("Contrast");
                contrast = new FlatSlider { Width = w + S(6), Height = S(26), Location = new Point(x - S(6), y), Enabled = false, AccessibleName = s.Name + " contrast" };
                box.Controls.Add(contrast);
                y += S(30);
            }
            if (caps.Has(Vcp.ColorPreset) && caps.Values(Vcp.ColorPreset).Count > 0)
            {
                label("Colour");
                preset = MakeCombo();
                preset.SetBounds(x, y + S(2), w, S(24));
                preset.Enabled = false;
                preset.AccessibleName = s.Name + " colour preset";
                foreach (var v in caps.Values(Vcp.ColorPreset)) preset.Items.Add(new Choice(v, PresetName(v)));
                box.Controls.Add(preset);
                y += S(32);
            }
            if (caps.Has(Vcp.Input) && caps.Values(Vcp.Input).Count > 0)
            {
                label("Input");
                input = MakeCombo();
                input.SetBounds(x, y + S(2), w, S(24));
                input.Enabled = false;
                input.AccessibleName = s.Name + " input source";
                foreach (var v in caps.Values(Vcp.Input)) input.Items.Add(new Choice(v, InputName(v)));
                box.Controls.Add(input);
                y += S(30);
                var note = MakeLabel("Switches this screen to another device.", Theme.Muted, 8f, false);
                note.Location = new Point(x, y);
                box.Controls.Add(note);
                y += S(20);
            }
            box.Height = y + S(6);

            // Read the current values on the monitor's pump, then fill in the controls.
            s.Run(delegate
            {
                uint c = 0, cmax = 0, p = 0, pm, i = 0, im;
                bool cOk = contrast != null && s.ReadVcp(Vcp.Contrast, out c, out cmax) && cmax > 0;
                bool pOk = preset != null && s.ReadVcp(Vcp.ColorPreset, out p, out pm);
                bool iOk = input != null && s.ReadVcp(Vcp.Input, out i, out im);
                if (!IsHandleCreated || IsDisposed) return;
                BeginInvoke((MethodInvoker)delegate
                {
                    if (box.IsDisposed) return;
                    if (contrast != null)
                    {
                        contrast.SetSilently(cOk ? (int)Math.Round(c * 100.0 / cmax) : 50);
                        contrast.Enabled = true;
                        uint max = cOk ? cmax : 100;
                        contrast.ValueChanged += delegate { s.SetVcp(Vcp.Contrast, (uint)Math.Round(contrast.Value * max / 100.0)); };
                    }
                    if (preset != null) Select(preset, pOk, (byte)(p & 0xFF), delegate(byte v) { s.SetVcp(Vcp.ColorPreset, v); });
                    if (input != null) Select(input, iOk, (byte)(i & 0xFF), delegate(byte v) { s.SetVcp(Vcp.Input, v); });
                });
            });
            return box;
        }

        // Some monitors report a current value outside their own list (the MSI MD342CQP says input 0x08 while
        // on DisplayPort). Show that honestly as "not reported" rather than picking a wrong name.
        static void Select(ComboBox combo, bool readOk, byte current, Action<byte> apply)
        {
            int match = -1;
            for (int k = 0; k < combo.Items.Count; k++)
                if (readOk && ((Choice)combo.Items[k]).Value == current) match = k;
            if (match < 0)
            {
                combo.Items.Insert(0, new Choice(0, "Current: not reported", false));
                match = 0;
            }
            combo.SelectedIndex = match;
            combo.Enabled = true;
            combo.SelectionChangeCommitted += delegate
            {
                var c = combo.SelectedItem as Choice;
                if (c != null && c.Real) apply(c.Value);
            };
        }

        class Choice
        {
            public readonly byte Value;
            public readonly bool Real;
            readonly string text;
            public Choice(byte value, string text, bool real = true) { Value = value; this.text = text; Real = real; }
            public override string ToString() { return text; }
        }

        static string PresetName(byte v)
        {
            switch (v)
            {
                case 0x01: return "sRGB";
                case 0x02: return "Native";
                case 0x03: return "4000 K";
                case 0x04: return "5000 K";
                case 0x05: return "6500 K";
                case 0x06: return "7500 K";
                case 0x07: return "8200 K";
                case 0x08: return "9300 K";
                case 0x09: return "10000 K";
                case 0x0A: return "11500 K";
                case 0x0B: return "User 1";
                case 0x0C: return "User 2";
                case 0x0D: return "User 3";
                default: return "Preset 0x" + v.ToString("X2");
            }
        }

        static string InputName(byte v)
        {
            switch (v)
            {
                case 0x01: return "VGA 1";
                case 0x02: return "VGA 2";
                case 0x03: return "DVI 1";
                case 0x04: return "DVI 2";
                case 0x05: return "Composite 1";
                case 0x06: return "Composite 2";
                case 0x07: return "S-Video 1";
                case 0x08: return "S-Video 2";
                case 0x0F: return "DisplayPort 1";
                case 0x10: return "DisplayPort 2";
                case 0x11: return "HDMI 1";
                case 0x12: return "HDMI 2";
                case 0x13: return "HDMI 3";
                case 0x1B: return "USB-C";
                default: return "Input 0x" + v.ToString("X2");
            }
        }

        int AverageLevel()
        {
            int sum = 0, n = 0;
            foreach (var s in screens)
                if (s.Controllable && s.Responding) { sum += Matching.ToLevel(Matching.CurveFor(s), s.Brightness); n++; }
            return n == 0 ? 50 : sum / n;
        }

        void UpdateAll()
        {
            if (allSlider == null) return;
            int sum = 0, n = 0;
            foreach (var r in rows) if (r.Slider.Enabled) { sum += Matching.ToLevel(Matching.CurveFor(r.Screen), r.Slider.Value); n++; }
            allSlider.SetSilently(n == 0 ? 0 : sum / n);
            allValue.Text = allSlider.Value + "%";
        }

        // Presets and the "All screens" slider set a shared level (matched per screen).
        public void SetAll(int level)
        {
            if (allSlider != null) { allSlider.Value = level; return; }
            foreach (var r in rows) if (r.Slider.Enabled) r.Slider.Value = Matching.ToPercent(Matching.CurveFor(r.Screen), level);
        }

        // The laptop screen changed by itself (keys, adaptive brightness): bring the other screens along.
        public void FollowLaptop(int laptopPercent)
        {
            Row laptop = null;
            foreach (var r in rows) if (r.Screen.Kind == "WMI") laptop = r;
            if (laptop == null) return;
            laptop.Screen.Brightness = laptopPercent;
            laptop.Slider.SetSilently(laptopPercent);
            laptop.Update();
            int level = Matching.ToLevel(Matching.CurveFor(laptop.Screen), laptopPercent);
            linking = true;
            foreach (var r in rows)
                if (r != laptop && r.Slider.Enabled) r.Slider.Value = Matching.ToPercent(Matching.CurveFor(r.Screen), level);
            linking = false;
            UpdateAll();
        }

        public void ShowAt(Point cursor)
        {
            anchor = cursor;
            Position();
            Show();
            Activate();
        }

        // Next to the notification area, on whichever screen and edge the taskbar is.
        void Position()
        {
            var screen = System.Windows.Forms.Screen.FromPoint(anchor);
            var b = screen.Bounds;
            var wa = screen.WorkingArea;
            int m = S(12), x, y;
            if (wa.Top > b.Top) { x = anchor.X - Width / 2; y = wa.Top + m; }                 // taskbar at the top
            else if (wa.Left > b.Left) { x = wa.Left + m; y = anchor.Y - Height / 2; }        // left
            else if (wa.Right < b.Right) { x = wa.Right - Width - m; y = anchor.Y - Height / 2; } // right
            else { x = wa.Right - Width - m; y = wa.Bottom - Height - m; }                      // bottom or auto-hide
            x = Math.Max(wa.Left + m, Math.Min(x, wa.Right - Width - m));
            y = Math.Max(wa.Top + m, Math.Min(y, wa.Bottom - Height - m));
            Location = new Point(x, y);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) foreach (var f in fonts.Values) f.Dispose();
            base.Dispose(disposing);
        }
    }
}
