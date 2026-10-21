#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true
#:property PublishAot=false
// Draws the app icon (a monitor with an amber sun on its screen) at every size the MSIX package and the exe need.
// Usage (from the repo root):  dotnet run --file tools/make-assets.cs
// Writes packaging/Assets/*.png, src/DisplayControl/app.ico and installer/wizard-*.bmp.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

static class MakeAssets
{
    static readonly Color Sun = Color.FromArgb(255, 185, 0);        // amber, reads on light and dark taskbars
    static readonly Color Plate = Color.FromArgb(15, 27, 45);       // navy backplate for Store logo / tiles

    static void Main()
    {
        string root = Directory.GetCurrentDirectory();
        string assets = Path.Combine(root, "packaging", "Assets");
        Directory.CreateDirectory(assets);

        foreach (int scale in new[] { 100, 200, 400 })
        {
            float k = scale / 100f;
            Save(Draw((int)(50 * k), (int)(50 * k), plate: true, fraction: 0.78f), assets, "StoreLogo.scale-" + scale + ".png");
            Save(Draw((int)(150 * k), (int)(150 * k), plate: false, fraction: 0.62f), assets, "Square150x150Logo.scale-" + scale + ".png");
            Save(Draw((int)(310 * k), (int)(150 * k), plate: false, fraction: 0.62f), assets, "Wide310x150Logo.scale-" + scale + ".png");
            Save(Draw((int)(44 * k), (int)(44 * k), plate: false, fraction: 0.92f), assets, "Square44x44Logo.scale-" + scale + ".png");
        }
        // Taskbar / Start / title-bar sizes. "altform-unplated" = drawn without the tile backplate.
        foreach (int size in new[] { 16, 24, 32, 48, 256 })
        {
            Save(Draw(size, size, plate: false, fraction: 1f), assets, "Square44x44Logo.targetsize-" + size + ".png");
            Save(Draw(size, size, plate: false, fraction: 1f), assets, "Square44x44Logo.targetsize-" + size + "_altform-unplated.png");
        }

        WriteIco(Path.Combine(root, "src", "DisplayControl", "app.ico"), new[] { 16, 20, 24, 32, 40, 48, 64, 256 });

        // Installer wizard corner image (Inno Setup wants 24-bit BMP; the second size is for high-DPI screens).
        string installer = Path.Combine(root, "installer");
        Directory.CreateDirectory(installer);
        foreach (int size in new[] { 55, 110 })
            using (var icon = Draw(size, size, plate: false, fraction: 0.92f))
            using (var bmp = new Bitmap(size, size, PixelFormat.Format24bppRgb))
            {
                using (var g = Graphics.FromImage(bmp)) { g.Clear(Color.White); g.DrawImage(icon, 0, 0, size, size); }
                bmp.Save(Path.Combine(installer, "wizard-small-" + size + ".bmp"), ImageFormat.Bmp);
            }
        // Large image on the wizard's first and last pages: the icon on the navy brand colour.
        foreach (int scale in new[] { 1, 2 })
        {
            int w = 164 * scale, h = 314 * scale;
            using (var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var bg = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.FromArgb(18, 32, 54), Color.FromArgb(44, 78, 120), 90f))
                        g.FillRectangle(bg, 0, 0, w, h);
                    using (var icon = Draw(120 * scale, 120 * scale, plate: false, fraction: 1f))
                        g.DrawImage(icon, (w - icon.Width) / 2, h / 2 - icon.Height + 10 * scale);
                    using (var font = new Font("Segoe UI Semibold", 15f * scale, GraphicsUnit.Pixel))
                    using (var white = new SolidBrush(Color.White))
                    using (var fmt = new StringFormat { Alignment = StringAlignment.Center })
                        g.DrawString("Display Control", font, white, new RectangleF(0, h / 2 + 22 * scale, w, 30 * scale), fmt);
                    using (var font = new Font("Segoe UI", 11f * scale, GraphicsUnit.Pixel))
                    using (var soft = new SolidBrush(Color.FromArgb(190, 210, 235)))
                    using (var fmt = new StringFormat { Alignment = StringAlignment.Center })
                        g.DrawString("Brightness and colour\nfor every screen", font, soft, new RectangleF(8 * scale, h / 2 + 50 * scale, w - 16 * scale, 40 * scale), fmt);
                }
                bmp.Save(Path.Combine(installer, "wizard-large-" + w + ".bmp"), ImageFormat.Bmp);
            }
        }
        Console.WriteLine("Assets written to " + assets);
    }

    static readonly Color Frame = Color.FromArgb(52, 66, 92);        // slate monitor frame, reads on light and dark
    static readonly Color FrameOnPlate = Color.FromArgb(226, 234, 244);
    static readonly Color ScreenTop = Color.FromArgb(28, 62, 110);    // deep blue screen
    static readonly Color ScreenBottom = Color.FromArgb(18, 38, 70);

    // A monitor with the amber sun on its screen. fraction: how much of the shorter side the monitor fills.
    static Bitmap Draw(int w, int h, bool plate, float fraction)
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

            float d = Math.Min(w, h) * fraction;
            float x0 = (w - d) / 2f, y0 = (h - d) / 2f;
            bool tiny = d < 34;   // rays turn to mush below ~32 px
            var frame = plate ? FrameOnPlate : Frame;
            float stroke = Math.Max(1.2f, d * (tiny ? 0.085f : 0.055f));

            // Screen
            var screen = new RectangleF(x0 + d * 0.05f, y0 + d * 0.10f, d * 0.90f, d * 0.60f);
            using (var path = RoundRect(screen, d * 0.08f))
            {
                using (var fill = new LinearGradientBrush(screen, ScreenTop, ScreenBottom, 90f)) g.FillPath(fill, path);
                using (var pen = new Pen(frame, stroke)) g.DrawPath(pen, path);
            }
            // Stand
            using (var b = new SolidBrush(frame))
            {
                g.FillRectangle(b, x0 + d * 0.45f, screen.Bottom, d * 0.10f, d * 0.12f);
                using (var foot = RoundRect(new RectangleF(x0 + d * 0.28f, y0 + d * 0.80f, d * 0.44f, Math.Max(1.5f, d * 0.07f)), d * 0.03f))
                    g.FillPath(b, foot);
            }
            // Sun on the screen
            float cx = screen.X + screen.Width / 2f, cy = screen.Y + screen.Height / 2f;
            float core = d * (tiny ? 0.15f : 0.10f);
            if (!tiny)
            {
                float inner = d * 0.155f, outer = d * 0.215f;
                using (var pen = new Pen(Sun, Math.Max(1f, d * 0.035f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4;
                        g.DrawLine(pen, cx + (float)Math.Cos(a) * inner, cy + (float)Math.Sin(a) * inner,
                                        cx + (float)Math.Cos(a) * outer, cy + (float)Math.Sin(a) * outer);
                    }
            }
            using (var b = new SolidBrush(Sun)) g.FillEllipse(b, cx - core, cy - core, core * 2, core * 2);
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
            using (var bmp = Draw(s, s, plate: false, fraction: 1f))
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
