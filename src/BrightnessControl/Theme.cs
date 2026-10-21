using System.Drawing;
using Microsoft.Win32;

namespace BrightnessControl
{
    // Colours follow the Windows app theme (Settings > Personalization > Colors).
    static class Theme
    {
        public static bool Dark { get; private set; }
        public static bool TaskbarDark { get; private set; }

        public static Color Back, Card, Text, Muted, Accent, Track, Border, Hover, Thumb, Warning;

        static Theme() { Refresh(); }

        // Re-reads the Windows setting. Returns true if anything changed.
        public static bool Refresh()
        {
            bool dark = ReadFlag("AppsUseLightTheme") == 0;
            bool taskbarDark = ReadFlag("SystemUsesLightTheme") == 0;
            bool changed = dark != Dark || taskbarDark != TaskbarDark || Back.IsEmpty;
            Dark = dark;
            TaskbarDark = taskbarDark;
            if (dark)
            {
                Back = Color.FromArgb(32, 32, 32);
                Card = Color.FromArgb(43, 43, 43);
                Text = Color.FromArgb(240, 240, 240);
                Muted = Color.FromArgb(160, 160, 160);
                Accent = Color.FromArgb(76, 194, 255);
                Track = Color.FromArgb(80, 80, 80);
                Border = Color.FromArgb(70, 70, 70);
                Hover = Color.FromArgb(60, 60, 60);
                Thumb = Color.FromArgb(69, 69, 69);
                Warning = Color.FromArgb(255, 185, 90);
            }
            else
            {
                Back = Color.FromArgb(243, 243, 243);
                Card = Color.FromArgb(251, 251, 251);
                Text = Color.FromArgb(26, 26, 26);
                Muted = Color.FromArgb(96, 96, 96);
                Accent = Color.FromArgb(0, 95, 184);
                Track = Color.FromArgb(200, 200, 200);
                Border = Color.FromArgb(215, 215, 215);
                Hover = Color.FromArgb(234, 234, 234);
                Thumb = Color.FromArgb(255, 255, 255);
                Warning = Color.FromArgb(157, 93, 0);
            }
            return changed;
        }

        // 1 = light, 0 = dark. Default to dark if the value is missing (older Windows builds).
        static int ReadFlag(string name)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = k == null ? null : k.GetValue(name);
                    return v is int ? (int)v : 0;
                }
            }
            catch { return 0; }
        }
    }
}
