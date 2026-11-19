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
            // Feet are hidden by the ball gown: show only tiny shoe tips behind
            // its intact hem rather than cutting the dress into moving halves.
            using (var shoe = new SolidBrush(Color.FromArgb(62, 39, 28)))
                for (int leg = 0; leg < 2; leg++)
                {
                    float lift = Math.Max(0, (float)Math.Sin(phase + leg * Math.PI)) * amount;
                    g.FillRectangle(shoe, image.Width / 2f + (leg == 0 ? -10 : 3), 174 - lift, 7, 3);
                }
            g.DrawImageUnscaled(image, 0, 0);
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
                float bend = (float)Math.Sin(t * Math.PI) * Math.Max(0, cycle);
                float dx = (cycle * ease + bend * .35f) * profile.Travel * amount * (sideView ? 2.4f : 1f);
                float dy = -Math.Max(0, cycle) * ease * profile.Lift * amount;
                g.DrawImage(image, new RectangleF(left + dx, y + dy, width, 1.1f),
                    new RectangleF(left, y, width, 1), GraphicsUnit.Pixel);
            }
        }
        g.DrawImageUnscaled(body, 0, 0);
    }

    public void Dispose() { body.Dispose(); }
}
