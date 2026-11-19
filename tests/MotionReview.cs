using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
internal static class MotionReview
{
    static int Main(string[] args)
    {
        string[] files = Directory.GetFiles(args[0], "*.png"); Array.Sort(files);
        using (var sheet = new Bitmap(1800, 1400))
        using (var g = Graphics.FromImage(sheet))
        using (var font = new Font("Arial", 10))
        {
            g.Clear(Color.FromArgb(218, 214, 203));
            for (int i = 0; i < files.Length; i++)
            using (var source = new Bitmap(files[i]))
            using (var image = new Bitmap(source))
            using (var motion = new WalkMotion(image, Path.GetFileName(files[i]), args.Length > 2 && args[2] == "side"))
            using (var baseline = new Bitmap(image.Width, image.Height))
            {
                using (var bg = Graphics.FromImage(baseline)) { bg.InterpolationMode = InterpolationMode.NearestNeighbor; bg.PixelOffsetMode = PixelOffsetMode.Half; bg.DrawImageUnscaled(image, 0, 0); }
                int changes = 0;
                for (int pose = 0; pose < 5; pose++)
                using (var frame = new Bitmap(image.Width, image.Height))
                using (var fg = Graphics.FromImage(frame))
                {
                    fg.InterpolationMode = InterpolationMode.NearestNeighbor;
                    fg.PixelOffsetMode = PixelOffsetMode.Half;
                    motion.Draw(fg, (float)((pose - 1) * Math.PI / 2), pose == 0 ? 0 : 1);
                    for (int y = 0; y < image.Height; y++)
                    for (int x = 0; x < image.Width; x++)
                    {
                        Color c = frame.GetPixel(x,y), b = baseline.GetPixel(x,y);
                        if ((c.A != 0 || b.A != 0) && c.ToArgb() != b.ToArgb())
                        {
                            if (pose == 0) throw new Exception("Rest changed: " + files[i]);
                            if (y < 100) throw new Exception("Upper body changed");
                            changes++;
                        }
                    }
                    g.DrawImageUnscaled(frame, i % 3 * 600 + pose * 118 + (118-image.Width)/2, i / 3 * 200 + 15);
                }
                g.DrawString(Path.GetFileName(files[i]).Substring(0,2), font, Brushes.Black, i % 3 * 600 + 4, i / 3 * 200 + 2);
                if (changes == 0) throw new Exception("No animation: " + files[i]);
                Console.WriteLine(Path.GetFileName(files[i]) + ": " + changes);
            }
            sheet.Save(args[1]);
        }
        return 0;
    }
}
