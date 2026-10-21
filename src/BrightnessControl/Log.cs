using System;
using System.IO;

namespace BrightnessControl
{
    // Detection log, so a monitor that goes missing on someone else's machine can be explained.
    // Note: under MSIX, %LOCALAPPDATA% is redirected into the package's own folder.
    static class Log
    {
        static readonly object gate = new object();
        public static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BrightnessControl", "detect.log");

        public static void Write(string line)
        {
            lock (gate)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    var file = new FileInfo(FilePath);
                    if (file.Exists && file.Length > 256 * 1024) file.Delete();
                    File.AppendAllText(FilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff  ") + line + Environment.NewLine);
                }
                catch { }
            }
        }
    }
}
