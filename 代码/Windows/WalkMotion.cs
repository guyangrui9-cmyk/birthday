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
    private readonly Bitmap image, body;
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
        image = source;
        sideView = side;
        profile = Profiles[file.Substring(0, 2)];
        body = source.Clone(new Rectangle(0, 0, source.Width, source.Height), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        // Preserve skirt sides; remove only the authored leg region.
        if (profile.Kind != "gown")
            for (int y = profile.Root; y < body.Height; y++)
                for (int x = 0; x < body.Width; x++)
                    body.SetPixel(x, y, Color.Transparent);
    }

    internal void Draw(Graphics g, float phase, float amount)
    {
        if (amount < .001f) { g.DrawImageUnscaled(image, 0, 0); return; }
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
                    float x = image.Width / 2f + (sideView ? cycle * 5 : leg == 0 ? -11 : 3);
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
                float ease = t * t * (3 - 2 * t);
                float swing = Math.Max(0, (float)Math.Cos(phase + leg * Math.PI));
                float bend = (float)Math.Sin(t * Math.PI) * swing;
                // Front/back depth steps keep the inseam anchored. Opposing
                // horizontal shifts used to tear an artificial gap in the cloth.
                float dx = sideView ? (float)Math.Round((cycle * ease + bend * .25f) * profile.Travel * amount * 3.2f) : 0;
                // Side walking is a planted forward/back leg swing, not marching.
                // Exposed calves deform from the hem, not just the shoe region.
                float vertical = sideView ? -swing * .45f : cycle * 1.2f - swing * profile.Lift * .4f;
                float dy = vertical * ease * amount;
                float nextT = Math.Min(1f, t + 1f / (image.Height - profile.Root - 1));
                float nextEase = nextT * nextT * (3 - 2 * nextT);
                float nextDy = vertical * nextEase * amount;
                float rowHeight = Math.Max(.4f, 1 + nextDy - dy + .25f);
                g.DrawImage(image, new RectangleF(left + dx, y + dy, width, rowHeight),
                    new RectangleF(left, y, width, 1), GraphicsUnit.Pixel);
            }
        }
        g.DrawImageUnscaled(body, 0, 0);
    }

    public void Dispose() { body.Dispose(); }
}
