using System.Drawing;
using System.Windows.Forms;

namespace DisplayControl
{
    // A plain colour square with the screen's name, used by the match wizards to compare screens. Never takes focus.
    class PatchForm : Form
    {
        public PatchForm(string name, Color color)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = color;
            Controls.Add(new Label
            {
                Text = name,
                Dock = DockStyle.Bottom,
                Height = 40,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(40, 40, 40),
                Font = new Font("Segoe UI", 12f)
            });
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        // Centered on a screen, 2/5 of its size, a little above the middle.
        public void PlaceOn(Rectangle screen)
        {
            int w = screen.Width * 2 / 5, h = screen.Height * 2 / 5;
            Bounds = new Rectangle(screen.Left + (screen.Width - w) / 2, screen.Top + screen.Height / 6, w, h);
        }
    }
}
