using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace BrightnessControl
{
    // "Match brightness" wizard. The reference screen (the laptop if there is one) is set to a dim and then a
    // bright level; the user adjusts every other screen until a grey square on each looks equally bright.
    // The two answers become each screen's matching curve.
    class MatchWizard : Form
    {
        static readonly int[] Levels = { 30, 80 };

        readonly Screen2 reference;
        readonly List<Screen2> others = new List<Screen2>();
        readonly Dictionary<Screen2, int> original = new Dictionary<Screen2, int>();
        readonly Dictionary<Screen2, int[]> answers = new Dictionary<Screen2, int[]>();
        readonly List<Form> patches = new List<Form>();
        readonly List<FlatSlider> sliders = new List<FlatSlider>();
        readonly Label step;
        readonly Button next, cancel;
        int stepIndex;

        public static bool CanRun(List<Screen2> screens)
        {
            int n = 0;
            foreach (var s in screens) if (s.Controllable && s.Responding) n++;
            return n >= 2;
        }

        public MatchWizard(List<Screen2> screens)
        {
            foreach (var s in screens)
                if (s.Controllable && s.Responding && s.Kind == "WMI") { reference = s; break; }
            foreach (var s in screens)
                if (s.Controllable && s.Responding)
                {
                    original[s] = s.Brightness;
                    if (reference == null) reference = s;
                    else if (s != reference) { others.Add(s); answers[s] = new int[Levels.Length]; }
                }

            Text = "Match brightness";
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
            ClientSize = new Size(440, 150 + others.Count * 58);

            step = new Label { Location = new Point(16, 14), Size = new Size(408, 78), ForeColor = Theme.Text };
            Controls.Add(step);
            int y = 98;
            foreach (var s in others)
            {
                Controls.Add(new Label { Text = s.Name, Location = new Point(16, y), AutoSize = true, ForeColor = Theme.Text });
                var slider = new FlatSlider { Location = new Point(10, y + 20), Size = new Size(420, 28), AccessibleName = s.Name + " brightness" };
                var screen = s;
                slider.ValueChanged += delegate { screen.Set(slider.Value); };
                Controls.Add(slider);
                sliders.Add(slider);
                y += 58;
            }
            cancel = MakeButton("Cancel", 236, y + 4);
            next = MakeButton("Next", 336, y + 4);
            cancel.Click += delegate { Close(); };
            next.Click += delegate { Next(); };
            AcceptButton = next;
            CancelButton = cancel;
        }

        Button MakeButton(string text, int x, int y)
        {
            var b = new Button { Text = text, Location = new Point(x, y), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, BackColor = Theme.Card, ForeColor = Theme.Text };
            b.FlatAppearance.BorderColor = Theme.Border;
            Controls.Add(b);
            return b;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Sit at the bottom of the reference screen so it doesn't cover the grey square.
            var area = reference.Bounds.IsEmpty ? System.Windows.Forms.Screen.PrimaryScreen.WorkingArea : System.Windows.Forms.Screen.FromRectangle(reference.Bounds).WorkingArea;
            Location = new Point(area.Left + (area.Width - Width) / 2, area.Bottom - Height - 24);
            ShowPatches();
            ShowStep();
        }

        void ShowPatches()
        {
            var all = new List<Screen2>(others);
            all.Insert(0, reference);
            foreach (var s in all)
            {
                if (s.Bounds.IsEmpty) continue;
                var p = new PatchForm(s == reference ? s.Name + " (reference)" : s.Name, Color.FromArgb(128, 128, 128));
                p.PlaceOn(s.Bounds);
                p.Show();
                patches.Add(p);
            }
        }

        void ShowStep()
        {
            int level = Levels[stepIndex];
            step.Text = "Step " + (stepIndex + 1) + " of " + Levels.Length + ": " + reference.Name + " is set to " + level + "%.\n\n" +
                        "Move each slider until the grey square on that screen looks as bright as the one on " + reference.Name + ".";
            reference.Set(level);
            for (int i = 0; i < others.Count; i++)
            {
                // Start from the current curve's guess, so recalibrating is quick.
                int guess = Matching.ToPercent(Settings.Current.For(others[i].Id).Curve, level);
                sliders[i].Value = guess;
                others[i].Set(guess);
            }
            next.Text = stepIndex == Levels.Length - 1 ? "Save" : "Next";
        }

        void Next()
        {
            for (int i = 0; i < others.Count; i++) answers[others[i]][stepIndex] = sliders[i].Value;
            if (stepIndex < Levels.Length - 1) { stepIndex++; ShowStep(); return; }

            Settings.Current.For(reference.Id).Curve = null;
            foreach (var s in others)
            {
                var points = new List<int[]>();
                for (int k = 0; k < Levels.Length; k++) points.Add(new[] { Levels[k], answers[s][k] });
                Settings.Current.For(s.Id).Curve = Matching.MakeCurve(points);
                Log.Write("Matched " + s.Name + ": " + string.Join(", ", Array.ConvertAll(points.ToArray(), p => p[0] + "->" + p[1])));
            }
            Settings.Current.MatchBrightness = true;
            Settings.Current.Save();
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            foreach (var p in patches) p.Close();
            foreach (var kv in original) kv.Key.Set(kv.Value);   // saved or cancelled: put everything back
            base.OnFormClosed(e);
        }
    }
}
