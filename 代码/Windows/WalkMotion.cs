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
                    g.FillRectangle(shoe, x, 175 + (cycle * 2 - lift) * amount, 8, 5 * amount);
                }
            return;
        }
        float middle = (profile.Left + profile.Right) / 2f;
        for (int leg = 0; leg < 2; leg++)
        {
            float cycle = (float)Math.Sin(phase + leg * Math.PI);
            float left = sideView ? 0 : leg == 0 ? 0 : middle;
            float width = sideView ? image.Width : leg == 0 ? middle : image.Width - middle;
            for (int y = profile.Root; y < image.Height; y++)
            {
                float t = (y - profile.Root) / (float)(image.Height - profile.Root - 1);
                // Continuous deformation has zero displacement and zero slope
                // at the hip/hem. Feet move mainly in depth, not sideways.
                float ease = t * t * (3 - 2 * t);
                float swing = Math.Max(0, (float)Math.Cos(phase + leg * Math.PI));
                float bend = (float)Math.Sin(t * Math.PI) * swing;
                float dx = (cycle * ease + bend * .65f) * profile.Travel * amount * (sideView ? 3.2f : 1f);
                float dy = (cycle * 2.5f - swing * profile.Lift) * ease * amount;
                float nextT = Math.Min(1f, t + 1f / (image.Height - profile.Root - 1));
                float nextEase = nextT * nextT * (3 - 2 * nextT);
                float nextDy = (cycle * 2.5f - swing * profile.Lift) * nextEase * amount;
                float rowHeight = Math.Max(.4f, 1 + nextDy - dy + .25f);
                g.DrawImage(image, new RectangleF(left + dx, y + dy, width, rowHeight),
                    new RectangleF(left, y, width, 1), GraphicsUnit.Pixel);
            }
        }
        g.DrawImageUnscaled(body, 0, 0);
    }

    public void Dispose() { body.Dispose(); }
}
