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
            using (var showRequest = new EventWaitHandle(false, EventResetMode.AutoReset, "BrightnessControl.Show"))
            {
                // Already running (e.g. launched again from Start): ask that copy to open its panel.
                if (!created) { showRequest.Set(); return; }
                ApplicationConfiguration.Initialize();
                var app = new App();
                ThreadPool.RegisterWaitForSingleObject(showRequest, delegate { app.ShowFromOtherInstance(); }, null, -1, false);
                Application.Run(app);
            }
        }
    }
}
