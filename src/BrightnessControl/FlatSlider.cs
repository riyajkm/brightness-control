using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace BrightnessControl
{
    // Flat 0-100 slider drawn by hand so it looks at home on Windows 11. Exposed to screen readers as a slider.
    class FlatSlider : Control
    {
        int value;
        bool dragging;
        public event EventHandler ValueChanged;

        public FlatSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.Slider;
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
                AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            }
        }

        public void SetSilently(int v)
        {
            value = Math.Max(0, Math.Min(100, v));
            Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
        }

        public bool Dragging { get { return dragging; } }

        float Scale1 { get { return DeviceDpi / 96f; } }
        int Pad { get { return (int)(10 * Scale1); } }

        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        void FromX(int x)
        {
            float w = Width - Pad * 2;
            Value = (int)Math.Round(Math.Max(0, Math.Min(1, (x - Pad) / w)) * 100);
        }

        protected override void OnMouseDown(MouseEventArgs e) { dragging = true; Focus(); FromX(e.X); }
        protected override void OnMouseMove(MouseEventArgs e) { if (dragging) FromX(e.X); }
        protected override void OnMouseUp(MouseEventArgs e) { dragging = false; }
        protected override void OnMouseWheel(MouseEventArgs e) { Value += e.Delta > 0 ? 5 : -5; }
        protected override bool IsInputKey(Keys k)
        {
            return k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down || base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Left: case Keys.Down: Value -= 1; break;
                case Keys.Right: case Keys.Up: Value += 1; break;
                case Keys.PageDown: Value -= 10; break;
                case Keys.PageUp: Value += 10; break;
                case Keys.Home: Value = 0; break;
                case Keys.End: Value = 100; break;
                default: return;
            }
            e.Handled = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Card);
            float k = Scale1;
            int cy = Height / 2;
            float w = Width - Pad * 2;
            float x = Pad + w * value / 100f;
            var accent = Enabled ? Theme.Accent : Theme.Muted;
            using (var p = new Pen(Theme.Track, 4 * k) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(p, Pad, cy, Pad + w, cy);
            using (var p = new Pen(accent, 4 * k) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(p, Pad, cy, x, cy);
            float r = 9 * k, ri = (Focused ? 6 : 5) * k;
            using (var b = new SolidBrush(Theme.Thumb)) g.FillEllipse(b, x - r, cy - r, r * 2, r * 2);
            using (var p = new Pen(Theme.Border, 1)) g.DrawEllipse(p, x - r, cy - r, r * 2, r * 2);
            using (var b = new SolidBrush(accent)) g.FillEllipse(b, x - ri, cy - ri, ri * 2, ri * 2);
        }

        protected override AccessibleObject CreateAccessibilityInstance() { return new SliderAccessible(this); }

        class SliderAccessible : ControlAccessibleObject
        {
            readonly FlatSlider owner;
            public SliderAccessible(FlatSlider owner) : base(owner) { this.owner = owner; }
            public override AccessibleRole Role { get { return AccessibleRole.Slider; } }
            public override string Value
            {
                get { return owner.Value.ToString(CultureInfo.CurrentCulture) + "%"; }
                set
                {
                    int v;
                    if (int.TryParse((value ?? "").TrimEnd('%', ' '), NumberStyles.Integer, CultureInfo.CurrentCulture, out v)) owner.Value = v;
                }
            }
        }
    }
}
