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
    private readonly Bitmap image, body, original, arms;
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
        original = source;
        image = source.Clone(new Rectangle(0,0,source.Width,source.Height), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        arms = new Bitmap(source.Width,source.Height);
        armScale = file.StartsWith("04-") ? .2f : file.StartsWith("08-") ? .45f : 1f;
        sideArm = side && !file.StartsWith("04-");
        sideView = side;
        profile = Profiles[file.Substring(0, 2)];
        var mask=new bool[source.Width,source.Height];
        for(int y=103;y<143;y++) for(int x=2;x<source.Width-2;x++)
        {
            Color c=source.GetPixel(x,y);
            bool region=side ? x>source.Width*.48 && x<source.Width*.9 : x<source.Width*.34 || x>source.Width*.66;
            if(region && c.A>200 && c.R>180 && c.G>105 && c.G<235 && c.B<220 && c.R-c.G>12 && c.G-c.B>7)
                for(int yy=Math.Max(100,y-6);yy<=y;yy++) for(int xx=x-1;xx<=x+1;xx++) mask[xx,yy]=true;
        }
        if(sideArm)
        {
            Array.Clear(mask,0,mask.Length);
            using(var outline=new System.Drawing.Drawing2D.GraphicsPath())
            {
                outline.AddPolygon(new PointF[]{new PointF(source.Width*.56f,84),new PointF(source.Width*.66f,91),new PointF(source.Width*.63f,110),new PointF(source.Width*.59f,132),new PointF(source.Width*.44f,132),new PointF(source.Width*.40f,114),new PointF(source.Width*.43f,92)});
                for(int y=84;y<133;y++) for(int x=1;x<source.Width-1;x++)
                    mask[x,y]=outline.IsVisible(x,y) && source.GetPixel(x,y).A>0;
            }
        }
        for(int y=84;y<143;y++) for(int x=1;x<source.Width-1;x++) if(mask[x,y])
        {
            arms.SetPixel(x,y,source.GetPixel(x,y));
            int inward=sideArm ? 1 : side || x>source.Width/2 ? -1 : 1;
            int sample=x;
            while(sample>0 && sample<source.Width-1 && mask[sample,y]) sample+=inward;
                        Color fill=source.GetPixel(sample,y);
            if(fill.A<200)
            {
                for(int distance=1;distance<source.Width;distance++)
                {
                    int candidate=x+inward*distance;
                    if(candidate<0 || candidate>=source.Width) break;
                    if(!mask[candidate,y] && source.GetPixel(candidate,y).A>200)
                    { fill=source.GetPixel(candidate,y); break; }
                }
                if(fill.A<200) fill=source.GetPixel(x,y);
            }
            if(!sideArm || y>=100) image.SetPixel(x,y,fill);
        }
        body = image.Clone(new Rectangle(0, 0, source.Width, source.Height), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        // Preserve skirt sides; remove only the authored leg region.
        if (profile.Kind != "gown")
            for (int y = profile.Root; y < body.Height; y++)
                for (int x = 0; x < body.Width; x++)
                    body.SetPixel(x, y, Color.Transparent);
    }

    internal void Draw(Graphics g, float phase, float amount)
    {
        if (amount < .001f) { g.DrawImageUnscaled(original, 0, 0); return; }
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
            DrawArms(g,phase,amount); return;
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
        DrawArms(g,phase,amount);
    }

    private void DrawArms(Graphics g,float phase,float amount)
    {
        if(sideArm)
        {
            // Keep the shoulder and upper sleeve planted; deform the forearm
            // continuously from the elbow. No independent rigid cut can open.
            for(int y=84;y<143;y++)
            {
                float t=Math.Max(0,Math.Min(1,(y-100)/32f));
                float ease=t*t*(3-2*t);
                float dx=(float)Math.Round(-Math.Sin(phase)*4f*amount*armScale*ease);
                g.DrawImage(arms,new RectangleF(dx,y,arms.Width,1.1f),new RectangleF(0,y,arms.Width,1),GraphicsUnit.Pixel);
            }
            return;
        }
        int middle=image.Width/2;
        for(int side=0;side<2;side++)
        {
            int left=side==0 ? 0 : middle, width=side==0 ? middle : image.Width-middle;
            for(int y=100;y<143;y++)
            {
                float reach=Math.Min(1,(y-100)/30f);
                float swing=(float)Math.Sin(phase+(side==0 ? Math.PI : 0))*amount*armScale*reach;
                g.DrawImage(arms,new RectangleF(left+(sideView ? swing*2.2f : swing*.6f),y+(sideView ? 0 : swing*.8f),width,1.2f),new RectangleF(left,y,width,1),GraphicsUnit.Pixel);
            }
        }
    }
    public void Dispose() { body.Dispose(); image.Dispose(); arms.Dispose(); }
}
