using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BrightnessControl
{
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

        public bool Dragging { get { return dragging; } }

        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

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
            var accent = Enabled ? Theme.Accent : Theme.Muted;
            using (var p = new Pen(accent, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(p, Pad, cy, x, cy);
            using (var b = new SolidBrush(Color.FromArgb(69, 69, 69))) g.FillEllipse(b, x - 9, cy - 9, 18, 18);
            using (var b = new SolidBrush(accent)) g.FillEllipse(b, x - 5, cy - 5, 10, 10);
        }
    }
}
