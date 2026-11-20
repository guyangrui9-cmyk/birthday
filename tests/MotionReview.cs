using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
internal static class MotionReview
{
    static int InnerGap(Bitmap b, int center, int y)
    {
        if (b.GetPixel(center,y).A>128) return 0;
        int left=center,right=center;
        while(left>Math.Max(0,center-12) && b.GetPixel(left-1,y).A<=128) left--;
        while(right<Math.Min(b.Width-1,center+12) && b.GetPixel(right+1,y).A<=128) right++;
        return right-left+1;
    }
    static int Main(string[] args)
    {
        // Solid cloth must stay joined while its two leg regions move.
        using(var cloth=new Bitmap(85,177))
        using(var cg=Graphics.FromImage(cloth))
        using(var frame=new Bitmap(85,177))
        using(var fg=Graphics.FromImage(frame))
        {
            cg.FillRectangle(Brushes.Navy,20,100,45,65);
            fg.InterpolationMode=InterpolationMode.NearestNeighbor;
            fg.PixelOffsetMode=PixelOffsetMode.Half;
            using(var motion=new WalkMotion(cloth,"16-test.png")) motion.Draw(fg,(float)(Math.PI*1.5),1);
            if(frame.GetPixel(42,150).A<240) throw new Exception("Solid trouser cloth split during the outward step");
        }
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
                int changes = 0, legChanges = 0, armChanges = 0, upperArmChanges = 0;
                var profile = WalkMotion.Profiles[Path.GetFileName(files[i]).Substring(0,2)];
                if(!motion.ArmWarpIsContinuous()) throw new Exception("Arm deformation folds or disconnects: "+files[i]);
                for (int pose = 0; pose < 17; pose++)
                using (var frame = new Bitmap(image.Width, image.Height))
                using (var fg = Graphics.FromImage(frame))
                {
                    fg.InterpolationMode = InterpolationMode.NearestNeighbor;
                    fg.PixelOffsetMode = PixelOffsetMode.Half;
                    motion.Draw(fg, (float)((pose - 1) * Math.PI / 8), pose == 0 ? 0 : 1);
                    for (int y = 0; y < image.Height; y++)
                    for (int x = 0; x < image.Width; x++)
                    {
                        Color c = frame.GetPixel(x,y), b = baseline.GetPixel(x,y);
                        if ((c.A != 0 || b.A != 0) && c.ToArgb() != b.ToArgb())
                        {
                            if (pose == 0) throw new Exception("Rest changed: " + files[i]);
                            if (y < 80) throw new Exception("Head changed: "+files[i]+" y="+y);
                            changes++;
                            if(y>=84 && y<110) armChanges++;
                            if(y>=88 && y<100) upperArmChanges++;
                            if (y > profile.Root + 3 && y < image.Height - 12) legChanges++;
                        }
                        int center=image.Width/2;
                        if (args.Length <= 2 && pose > 0 && y > profile.Root+2 && y < image.Height-10 && x == center)
                        {
                            int natural=0;
                            for(int near=y-2;near<=y+2;near++) natural=Math.Max(natural,InnerGap(baseline,center,near));
                            if(natural<9 && InnerGap(frame,center,y)>natural+1)
                                throw new Exception("Leg gap widened beyond the original silhouette: " + files[i] + " y=" + y);
                        }
                        if (args.Length <= 2 && pose > 0 && y >= profile.Root && y < image.Height-10 && x == center &&
                            baseline.GetPixel(center-1,y).A>240 && baseline.GetPixel(center,y).A>240 && baseline.GetPixel(center+1,y).A>240 && c.A<32)
                            throw new Exception("Opened a transparent garment seam: " + files[i] + " y=" + y);
                    }
                    if(pose==0 || (pose-1)%4==0)
                        g.DrawImageUnscaled(frame, i % 3 * 600 + (pose==0 ? 0 : 1+(pose-1)/4) * 118 + (118-image.Width)/2, i / 3 * 200 + 15);
                }
                g.DrawString(Path.GetFileName(files[i]).Substring(0,2), font, Brushes.Black, i % 3 * 600 + 4, i / 3 * 200 + 2);
                if(args.Length>2 && args[2]=="side" && !Path.GetFileName(files[i]).StartsWith("04-") && upperArmChanges<8) throw new Exception("Upper arm stayed fixed: "+files[i]);
                if (changes == 0) throw new Exception("No animation: " + files[i]);
                if(args.Length>2 && args[2]=="side" && !Path.GetFileName(files[i]).StartsWith("04-") && armChanges<16)
                    throw new Exception("Sleeved upper arm did not visibly swing: " + files[i]);
                if (profile.Root < 150 && legChanges == 0) throw new Exception("Only shoes moved, calf stayed fixed: " + files[i]);
                Console.WriteLine(Path.GetFileName(files[i]) + ": " + changes);
            }
            sheet.Save(args[1]);
        }
        return 0;
    }
}
