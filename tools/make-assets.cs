#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true
#:property PublishAot=false
// Draws the app icon (an amber sun) at every size the MSIX package and the exe need.
// Usage (from the repo root):  dotnet run --file tools/make-assets.cs
// Writes packaging/Assets/*.png and src/BrightnessControl/app.ico.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

static class MakeAssets
{
    static readonly Color Sun = Color.FromArgb(255, 185, 0);        // amber, reads on light and dark taskbars
    static readonly Color SunEdge = Color.FromArgb(214, 130, 0);    // darker rim so it holds up on white
    static readonly Color Plate = Color.FromArgb(15, 27, 45);       // navy backplate for Store logo / tiles

    static void Main()
    {
        string root = Directory.GetCurrentDirectory();
        string assets = Path.Combine(root, "packaging", "Assets");
        Directory.CreateDirectory(assets);

        foreach (int scale in new[] { 100, 200, 400 })
        {
            float k = scale / 100f;
            Save(Draw((int)(50 * k), (int)(50 * k), plate: true, sunFraction: 0.78f), assets, "StoreLogo.scale-" + scale + ".png");
            Save(Draw((int)(150 * k), (int)(150 * k), plate: false, sunFraction: 0.5f), assets, "Square150x150Logo.scale-" + scale + ".png");
            Save(Draw((int)(310 * k), (int)(150 * k), plate: false, sunFraction: 0.5f), assets, "Wide310x150Logo.scale-" + scale + ".png");
            Save(Draw((int)(44 * k), (int)(44 * k), plate: false, sunFraction: 0.86f), assets, "Square44x44Logo.scale-" + scale + ".png");
        }
        // Taskbar / Start / title-bar sizes. "altform-unplated" = drawn without the tile backplate.
        foreach (int size in new[] { 16, 24, 32, 48, 256 })
        {
            Save(Draw(size, size, plate: false, sunFraction: 1f), assets, "Square44x44Logo.targetsize-" + size + ".png");
            Save(Draw(size, size, plate: false, sunFraction: 1f), assets, "Square44x44Logo.targetsize-" + size + "_altform-unplated.png");
        }

        WriteIco(Path.Combine(root, "src", "BrightnessControl", "app.ico"), new[] { 16, 20, 24, 32, 40, 48, 64, 256 });
        Console.WriteLine("Assets written to " + assets);
    }

    // sunFraction: how much of the shorter side the sun (rays included) fills.
    static Bitmap Draw(int w, int h, bool plate, float sunFraction)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            if (plate)
                using (var path = RoundRect(new RectangleF(0, 0, w, h), Math.Min(w, h) * 0.18f))
                using (var b = new SolidBrush(Plate))
                    g.FillPath(b, path);

            float d = Math.Min(w, h) * sunFraction;
            float cx = w / 2f, cy = h / 2f, r = d / 2f;
            bool tiny = d < 20;
            float ray = r * (tiny ? 0.15f : 0.13f);                // ray thickness
            float inner = r * 0.66f, outer = r - ray / 2f;
            float core = r * (tiny ? 0.5f : 0.46f);

            using (var pen = new Pen(Sun, ray) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4;
                    g.DrawLine(pen, cx + (float)Math.Cos(a) * inner, cy + (float)Math.Sin(a) * inner,
                                    cx + (float)Math.Cos(a) * outer, cy + (float)Math.Sin(a) * outer);
                }
            using (var b = new SolidBrush(Sun)) g.FillEllipse(b, cx - core, cy - core, core * 2, core * 2);
            if (!plate && !tiny)
                using (var p = new Pen(SunEdge, Math.Max(1f, r * 0.04f))) g.DrawEllipse(p, cx - core, cy - core, core * 2, core * 2);
        }
        return bmp;
    }

    static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    static void Save(Bitmap bmp, string dir, string name)
    {
        using (bmp) bmp.Save(Path.Combine(dir, name), ImageFormat.Png);
    }

    // Multi-size .ico with PNG-compressed entries (supported since Windows Vista).
    static void WriteIco(string path, int[] sizes)
    {
        var images = new List<byte[]>();
        foreach (int s in sizes)
            using (var bmp = Draw(s, s, plate: false, sunFraction: 1f))
            using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); images.Add(ms.ToArray()); }

        using (var w = new BinaryWriter(File.Create(path)))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32);
                w.Write(images[i].Length); w.Write(offset);
                offset += images[i].Length;
            }
            foreach (var img in images) w.Write(img);
        }
    }
}
