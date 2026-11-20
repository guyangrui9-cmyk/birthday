using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;

internal sealed class WalkMotion : IDisposable
{
    internal sealed class Profile
    {
        internal string Kind;
        internal int Root, Left, Right;
        internal float Travel, Lift;
    }
    internal static readonly Dictionary<string, Profile> Profiles = ReadProfiles();
    private Bitmap image, body;
    private readonly Bitmap original;
    private readonly Dictionary<int,Tuple<Bitmap,Bitmap>> armPoses=new Dictionary<int,Tuple<Bitmap,Bitmap>>();
    private readonly int wristEnd;
    private readonly Bitmap jacketBody, jacketArm;
    private readonly float armScale;
    private readonly bool sideArm;
    private readonly Profile profile;
    private readonly bool sideView;

    private static Dictionary<string, Profile> ReadProfiles()
    {
        var result = new Dictionary<string, Profile>();
        using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("walk-profiles.csv")))
        {
            reader.ReadLine();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (String.IsNullOrWhiteSpace(line)) continue;
                string[] v = line.Split(',');
                result.Add(v[0], new Profile { Kind = v[1], Root = int.Parse(v[2]), Left = int.Parse(v[3]), Right = int.Parse(v[4]),
                    Travel = float.Parse(v[5], CultureInfo.InvariantCulture), Lift = float.Parse(v[6], CultureInfo.InvariantCulture) });
            }
        }
        return result;
    }

    internal WalkMotion(Bitmap source, string file) : this(source, file, false) { }

    internal WalkMotion(Bitmap source, string file, bool side)
    {
        original=source; image=source;
        sideView=side; sideArm=side && !file.StartsWith("04-");
        armScale=file.StartsWith("04-") ? .1f : file.StartsWith("08-") ? .45f : 1f;
        profile=Profiles[file.Substring(0,2)];
        int[] ends={127,124,123,119,129,120,125,120,120,123,117,125,121,124,122,121,122,121,119,123,125};
        wristEnd=ends[int.Parse(file.Substring(0,2))-1];
        if(side && (file.StartsWith("17-") || file.StartsWith("18-")))
        {
            jacketBody=source.Clone(new Rectangle(0,0,source.Width,source.Height),System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            jacketArm=new Bitmap(25,49);
            using(var outline=new System.Drawing.Drawing2D.GraphicsPath())
            {
                outline.AddPolygon(new PointF[]{new PointF(35,79),new PointF(43,87),new PointF(40,109),new PointF(35,125),new PointF(26,125),new PointF(23,120),new PointF(22,108),new PointF(27,85)});
                                bool bareArm=file.StartsWith("18-");
                var bareMask=new bool[source.Width,source.Height];
                if(bareArm)
                    for(int y=79;y<123;y++) for(int x=22;x<44;x++)
                    {
                        Color skin=source.GetPixel(x,y);
                        if(skin.A>200 && skin.R>185 && skin.R-skin.G>25 && skin.G-skin.B>10)
                            for(int edge=Math.Max(20,x-1);edge<=Math.Min(44,x+1);edge++)
                            {
                                Color neighbor=source.GetPixel(edge,y);
                                if(edge==x || (neighbor.R<75 && neighbor.G<75 && neighbor.B<75)) bareMask[edge,y]=true;
                            }
                    }
                for(int y=79;y<126;y++) for(int x=20;x<45;x++) if(bareArm ? bareMask[x,y] : outline.IsVisible(x,y))
                {
                    jacketArm.SetPixel(x-20,y-79,source.GetPixel(x,y));
                    if(y>=86) jacketBody.SetPixel(x,y,x>=(bareArm ? 40 : 32) ? source.GetPixel(44,y) : Color.Transparent);
                }
            }
        }
    }

    private float ArmOffset(float x,int y,float swing)
    {
        float t=(y-84f)/(wristEnd-84f);
        float anchor=(float)Math.Sin(Math.Min(1,t)*Math.PI/2);
        float fade=Math.Min(1,(wristEnd-y)/5f);
        float center=sideView ? original.Width*.53f : x<original.Width/2f ? original.Width*.22f : original.Width*.78f;
        float radius=original.Width*(sideView ? .24f : .18f);
        float d=Math.Abs(x-center)/radius;
        float weight=d>=1 ? 0 : (1-d*d)*(1-d*d);
        float direction=sideView || x<original.Width/2f ? -1 : 1;
        return direction*swing*anchor*fade*weight*(sideView ? 5.2f : 2.1f);
    }

    internal bool ArmWarpIsContinuous()
    {
        for(int y=84;y<wristEnd;y++) for(int sign=-1;sign<=1;sign+=2)
            for(int x=0;x<original.Width-1;x++)
            {
                float span=1+ArmOffset(x+1,y,sign)-ArmOffset(x,y,sign);
                if(span<=.1f || span>=1.9f) return false;
            }
        return true;
    }

    private void PrepareArmPose(float phase,float amount)
    {
        int pose=((int)Math.Round(phase/(Math.PI*2)*32)%32+32)%32;
        int strength=(int)Math.Round(amount*4);
        int key=pose*5+strength;
        Tuple<Bitmap,Bitmap> cached;
        if(!armPoses.TryGetValue(key,out cached))
        {
            if(armPoses.Count>=40) { foreach(var pair in armPoses.Values){pair.Item1.Dispose();pair.Item2.Dispose();} armPoses.Clear(); }
            var warped=original.Clone(new Rectangle(0,0,original.Width,original.Height),System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            float swing=(float)Math.Sin(pose*Math.PI*2/32)*strength/4f*armScale;
            // Inverse sampling defines every destination pixel. A continuous,
            // monotone warp cannot open a cutout seam or require torso inpainting.
            for(int y=84;y<wristEnd && jacketBody==null;y++) for(int x=0;x<original.Width;x++)
            {
                float sx=x;
                for(int iteration=0;iteration<10;iteration++) sx=x-ArmOffset(sx,y,swing);
                int sample=Math.Max(0,Math.Min(original.Width-1,(int)Math.Round(sx)));
                warped.SetPixel(x,y,original.GetPixel(sample,y));
            }
            if(jacketBody!=null) { warped.Dispose(); warped=jacketBody.Clone(new Rectangle(0,0,jacketBody.Width,jacketBody.Height),System.Drawing.Imaging.PixelFormat.Format32bppArgb); }
            var upper=warped.Clone(new Rectangle(0,0,warped.Width,warped.Height),System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            if(profile.Kind!="gown") for(int y=profile.Root;y<upper.Height;y++) for(int x=0;x<upper.Width;x++) upper.SetPixel(x,y,Color.Transparent);
            cached=Tuple.Create(warped,upper);armPoses.Add(key,cached);
        }
        image=cached.Item1;body=cached.Item2;
    }
    internal void Draw(Graphics g, float phase, float amount)
    {
        if (amount < .001f) { g.DrawImageUnscaled(original, 0, 0); return; }
        PrepareArmPose(phase,amount);
        if (profile.Kind == "gown")
        {
            g.DrawImageUnscaled(image, 0, 0);
            // Feet are hidden by the ball gown: show only tiny shoe tips behind
            // its intact hem rather than cutting the dress into moving halves.
            using (var shoe = new SolidBrush(Color.FromArgb(62, 39, 28)))
                for (int leg = 0; leg < 2; leg++)
                {
                    float cycle = (float)Math.Sin(phase + leg * Math.PI);
                    float lift = Math.Max(0, (float)Math.Cos(phase + leg * Math.PI));
                    float x = image.Width / 2f + (sideView ? cycle * 6 : leg == 0 ? -11 : 3);
                    g.FillRectangle(shoe, x, 175 + (sideView ? 0 : cycle - lift * .3f) * amount, 8, 5 * amount);
                }
            return;
        }
        int middle = image.Width / 2;
        for (int leg = 0; leg < 2; leg++)
        {
            float cycle = (float)Math.Sin(phase + leg * Math.PI);
            float left = sideView ? 0 : leg == 0 ? 0 : middle - 1;
            float width = sideView ? image.Width : leg == 0 ? middle + 1 : image.Width - middle + 1;
            for (int y = profile.Root; y < image.Height; y++)
            {
                float t = (y - profile.Root) / (float)(image.Height - profile.Root - 1);
                // Continuous deformation has zero displacement and zero slope
                // at the hip/hem. Feet move mainly in depth, not sideways.
                float ease = sideView ? t : t * t * (3 - 2 * t);
                float swing = Math.Max(0, (float)Math.Cos(phase + leg * Math.PI));
                float bend = (float)Math.Sin(t * Math.PI) * swing;
                // Front/back depth steps keep the inseam anchored. Opposing
                // horizontal shifts used to tear an artificial gap in the cloth.
                float dx = sideView ? (float)Math.Round((cycle * ease + bend * .25f) * profile.Travel * amount * 3.8f) : 0;
                // Side walking is a planted forward/back leg swing, not marching.
                // Exposed calves deform from the hem, not just the shoe region.
                float vertical = sideView ? -swing * .45f : cycle * 1.45f - swing * profile.Lift * .46f;
                float dy = vertical * ease * amount;
                float nextT = Math.Min(1f, t + 1f / (image.Height - profile.Root - 1));
                float nextEase = sideView ? nextT : nextT * nextT * (3 - 2 * nextT);
                float nextDy = vertical * nextEase * amount;
                float rowHeight = Math.Max(.4f, 1 + nextDy - dy + .25f);
                g.DrawImage(image, new RectangleF(left + dx, y + dy, width, rowHeight),
                    new RectangleF(left, y, width, 1), GraphicsUnit.Pixel);
            }
        }
        g.DrawImageUnscaled(body, 0, 0);
        if(jacketArm!=null)
        {
            var state=g.Save();
            g.SetClip(new Rectangle(12,80,50,56),System.Drawing.Drawing2D.CombineMode.Intersect);
            g.TranslateTransform(36,82);
            g.RotateTransform(-(float)Math.Sin(phase)*11f*amount);
            g.DrawImage(jacketArm,new RectangleF(-16,-3,25,49),new RectangleF(0,0,25,49),GraphicsUnit.Pixel);
            g.Restore(state);
        }

    }

    public void Dispose() { foreach(var pair in armPoses.Values){pair.Item1.Dispose();pair.Item2.Dispose();} armPoses.Clear(); if(jacketBody!=null)jacketBody.Dispose();if(jacketArm!=null)jacketArm.Dispose(); }
}
