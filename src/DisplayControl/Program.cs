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

        // Command line (used by the installer):
        //   --exit       close a running copy cleanly
        //   --uninstall  reset matched colours and remove the startup entry (in the running copy, or here if none)
        [STAThread]
        static void Main(string[] args)
        {
            bool exitRequest = Array.IndexOf(args, "--exit") >= 0;
            bool uninstall = Array.IndexOf(args, "--uninstall") >= 0;

            // A bug should never take the tray app down: log it, tell the user once, keep running.
            AppInfo.MigrateFromBrightnessControl();   // before Settings or Log are first touched
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Report(e.Exception); };
            TaskScheduler.UnobservedTaskException += delegate(object s, UnobservedTaskExceptionEventArgs e) { Report(e.Exception); e.SetObserved(); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Log.Write("Fatal error: " + e.ExceptionObject); };

            bool created;
            using (var mutex = new Mutex(true, "DisplayControl.SingleInstance", out created))
            using (var showRequest = new EventWaitHandle(false, EventResetMode.AutoReset, "DisplayControl.Show"))
            using (var exitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "DisplayControl.Exit"))
            using (var uninstallSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "DisplayControl.Uninstall"))
            {
                if (!created)
                {
                    // Already running. A plain launch (e.g. from Start) asks that copy to open its panel.
                    if (!exitRequest && !uninstall) { showRequest.Set(); return; }
                    // The installer waits until the running copy has finished and closed.
                    (uninstall ? uninstallSignal : exitSignal).Set();
                    WaitForExit(mutex, uninstall ? 30000 : 10000);
                    return;
                }
                if (exitRequest) return;   // nothing running: nothing to close
                if (uninstall) { Lifecycle.UninstallWithoutUi(); return; }

                ApplicationConfiguration.Initialize();
                app = new App();
                ThreadPool.RegisterWaitForSingleObject(showRequest, delegate { app.ShowFromOtherInstance(); }, null, -1, false);
                ThreadPool.RegisterWaitForSingleObject(exitSignal, delegate { app.ExitFromSignal(); }, null, -1, false);
                ThreadPool.RegisterWaitForSingleObject(uninstallSignal, delegate { app.UninstallFromSignal(); }, null, -1, false);
                Application.Run(app);
            }
        }

        // The running copy owns the mutex until it exits; acquiring it means it has gone.
        static void WaitForExit(Mutex mutex, int timeoutMs)
        {
            try { if (mutex.WaitOne(timeoutMs)) mutex.ReleaseMutex(); }
            catch (AbandonedMutexException) { mutex.ReleaseMutex(); }   // it exited without releasing; also fine
            catch (Exception) { }
        }

        static void Report(Exception e)
        {
            Log.Write("Unexpected error: " + e);
            if (app != null) app.NotifyError();
        }
    }
}
