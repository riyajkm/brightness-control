// Display Control - tray app for adjusting monitor brightness on Windows.
// External monitors use DDC/CI (dxva2.dll); the laptop panel uses WMI.
// Built with .NET 10 (Windows Forms).

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DisplayControl
{
    static class Program
    {
        static App app;

        [STAThread]
        static void Main()
        {
            // A bug should never take the tray app down: log it, tell the user once, keep running.
            AppInfo.MigrateFromBrightnessControl();   // before Settings or Log are first touched
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Report(e.Exception); };
            TaskScheduler.UnobservedTaskException += delegate(object s, UnobservedTaskExceptionEventArgs e) { Report(e.Exception); e.SetObserved(); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Log.Write("Fatal error: " + e.ExceptionObject); };

            bool created;
            using (var mutex = new Mutex(true, "DisplayControl.SingleInstance", out created))
            using (var showRequest = new EventWaitHandle(false, EventResetMode.AutoReset, "DisplayControl.Show"))
            {
                // Already running (e.g. launched again from Start): ask that copy to open its panel.
                if (!created) { showRequest.Set(); return; }
                ApplicationConfiguration.Initialize();
                app = new App();
                ThreadPool.RegisterWaitForSingleObject(showRequest, delegate { app.ShowFromOtherInstance(); }, null, -1, false);
                Application.Run(app);
            }
        }

        static void Report(Exception e)
        {
            Log.Write("Unexpected error: " + e);
            if (app != null) app.NotifyError();
        }
    }
}
