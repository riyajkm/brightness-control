// Brightness Control - tray app for adjusting monitor brightness on Windows.
// External monitors use DDC/CI (dxva2.dll); the laptop panel uses WMI.
// Built with .NET 10 (Windows Forms).

using System;
using System.Threading;
using System.Windows.Forms;

namespace BrightnessControl
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, "BrightnessControl.SingleInstance", out created))
            {
                if (!created) return;
                ApplicationConfiguration.Initialize();
                Application.Run(new App());
            }
        }
    }
}
