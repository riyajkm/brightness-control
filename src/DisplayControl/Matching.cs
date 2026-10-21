using System;
using System.Collections.Generic;

namespace DisplayControl
{
    // Brightness matching: one shared "level" (0-100) maps to a different % on each monitor, so screens with
    // different maximum brightness look alike. Each monitor's curve comes from the match wizard as
    // [level, percent] points; between points it's linear, beyond them it extends the nearest segment.
    // A monitor without a curve uses level = percent.
    static class Matching
    {
        public static List<int[]> CurveFor(Screen2 s)
        {
            if (!Settings.Current.MatchBrightness || s == null) return null;
            var curve = Settings.Current.For(s.Id).Curve;
            return curve != null && curve.Count >= 2 ? curve : null;
        }

        public static int ToPercent(List<int[]> curve, int level)
        {
            if (curve == null || curve.Count < 2) return Clamp(level);
            return Clamp((int)Math.Round(Interpolate(curve, level, 0, 1)));
        }

        public static int ToLevel(List<int[]> curve, int percent)
        {
            if (curve == null || curve.Count < 2) return Clamp(percent);
            return Clamp((int)Math.Round(Interpolate(curve, percent, 1, 0)));
        }

        // Sorts the points and forces them to rise, so a shared level never maps to a dimmer % as it goes up.
        public static List<int[]> MakeCurve(IEnumerable<int[]> points)
        {
            var list = new List<int[]>();
            foreach (var p in points) list.Add(new[] { Clamp(p[0]), Clamp(p[1]) });
            list.Sort((a, b) => a[0].CompareTo(b[0]));
            for (int i = 1; i < list.Count; i++)
            {
                if (list[i][0] <= list[i - 1][0]) list[i][0] = Math.Min(100, list[i - 1][0] + 1);
                if (list[i][1] <= list[i - 1][1]) list[i][1] = Math.Min(100, list[i - 1][1] + 1);
            }
            return list;
        }

        // x/y pick which column is input and which is output (lets the same code invert the curve).
        static double Interpolate(List<int[]> curve, int input, int x, int y)
        {
            int n = curve.Count;
            int seg = 0;
            while (seg < n - 2 && input > curve[seg + 1][x]) seg++;
            var a = curve[seg];
            var b = curve[seg + 1];
            if (b[x] == a[x]) return a[y];
            return a[y] + (double)(input - a[x]) * (b[y] - a[y]) / (b[x] - a[x]);
        }

        static int Clamp(int v) { return Math.Max(0, Math.Min(100, v)); }
    }
}
