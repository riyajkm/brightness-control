using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace BrightnessControl
{
    // Per-monitor settings, keyed by Screen2.Id.
    class MonitorSettings
    {
        // Raw MCCS capabilities string; cached because fetching it takes over a second.
        public string Capabilities { get; set; }

        // Brightness-matching curve: pairs of [shared level, this monitor's %], from the match wizard.
        public List<int[]> Curve { get; set; }

        // Colour matching (white balance), -50..50 each. 0/0 = untouched.
        public int Warmth { get; set; }
        public int Tint { get; set; }

        // What the monitor had before colour matching first changed it, so Reset can put it back.
        public int? OriginalPreset { get; set; }
        public int[] OriginalGains { get; set; }
        public int[] GainMax { get; set; }

        // True once we've seen the monitor ignore RGB gain writes; it then gets software colour instead.
        public bool GainsUnsupported { get; set; }
    }

    class Settings
    {
        public bool FirstRunDone { get; set; }
        public bool FollowLaptop { get; set; }
        public bool MatchBrightness { get; set; } = true;
        public Dictionary<string, MonitorSettings> Monitors { get; set; } = new Dictionary<string, MonitorSettings>(StringComparer.OrdinalIgnoreCase);

        static readonly string FilePath = Path.Combine(AppInfo.DataFolder, "settings.json");
        static readonly object gate = new object();
        static readonly JsonSerializerOptions Options = new JsonSerializerOptions { WriteIndented = true };

        public static readonly Settings Current = Load();

        public MonitorSettings For(string id)
        {
            lock (gate)
            {
                MonitorSettings m;
                if (!Monitors.TryGetValue(id ?? "", out m)) { m = new MonitorSettings(); Monitors[id ?? ""] = m; }
                return m;
            }
        }

        static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath));
                    if (s != null)
                    {
                        s.Monitors = new Dictionary<string, MonitorSettings>(s.Monitors ?? new Dictionary<string, MonitorSettings>(), StringComparer.OrdinalIgnoreCase);
                        return s;
                    }
                }
            }
            catch (Exception e) { Log.Write("Settings unreadable, starting fresh: " + e.Message); }
            return new Settings();
        }

        // Safe to call from any thread. Writes to a temp file first so a crash can't leave half a file.
        public void Save()
        {
            lock (gate)
            {
                try
                {
                    Directory.CreateDirectory(AppInfo.DataFolder);
                    string tmp = FilePath + ".tmp";
                    File.WriteAllText(tmp, JsonSerializer.Serialize(this, Options));
                    File.Move(tmp, FilePath, true);
                }
                catch (Exception e) { Log.Write("Couldn't save settings: " + e.Message); }
            }
        }
    }
}
