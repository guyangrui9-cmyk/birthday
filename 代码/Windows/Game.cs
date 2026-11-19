using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal sealed partial class Game : Form
{
    private const int GridWidth = 1440;
    private const int GridHeight = 810;
    private readonly Bitmap frame = new Bitmap(GridWidth, GridHeight);
    private readonly Bitmap room = Asset("room.png");
    private readonly Bitmap map = Asset("map.png");
    private readonly Bitmap player = Asset("player.png");
    private readonly Bitmap companion = Asset("companion.png");
    private readonly HashSet<Keys> keys = new HashSet<Keys>();
    private readonly Timer timer = new Timer { Interval = 16 };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Font heading = new Font("Microsoft YaHei UI", 19, FontStyle.Bold);
    private readonly Font normal = new Font("Microsoft YaHei UI", 11);
    private static readonly Color Cream = Color.FromArgb(255, 237, 207);
    private static readonly RectangleF[] obstacles = {
        new RectangleF(.065f, .585f, .148f, .357f), new RectangleF(.715f, .59f, .213f, .34f),
        new RectangleF(.127f, .35f, .216f, .106f), new RectangleF(.88f, .38f, .11f, .17f),
        new RectangleF(.695f, .34f, .122f, .104f), new RectangleF(.392f, .792f, .108f, .094f), new RectangleF(.527f, .608f, .063f, .049f)
    };
    private static readonly PointF[] targets = { new PointF(.53f,.45f), new PointF(.715f,.71f), new PointF(.45f,.80f), new PointF(.75f,.475f) };
    private static readonly RectangleF[] hotspots = {
        new RectangleF(.36f,.065f,.32f,.32f), new RectangleF(.765f,.60f,.086f,.155f), new RectangleF(.39f,.75f,.11f,.14f), new RectangleF(.695f,.095f,.12f,.35f)
    };
    private static readonly string[] labels = { "旅行贴板", "生日礼物", "摸摸西施狗", "衣橱" };
    private PointF position = new PointF(.43f, .71f);
    private int facing = 1;
    private bool sideFacing;
    private bool backFacing;
    private float phase;
    private float stride;
    private double lastTick;

    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [STAThread]
    private static int Main(string[] args)
    {
        SetProcessDPIAware();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (Game game = new Game())
        {
            if (Array.IndexOf(args, "--self-test") >= 0)
                return game.SelfTest() ? 0 : 1;
            Application.Run(game);
        }
        return 0;
    }

    private Game()
    {
        LoadWardrobe();
        Text = "我们的小屋";
        ClientSize = new Size(1080, 690);
        MinimumSize = new Size(560, 400);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(36, 23, 21);
        DoubleBuffered = true;
        ResizeRedraw = true;
        KeyPreview = true;
        Deactivate += delegate { keys.Clear(); };
        timer.Tick += delegate {
            double now = clock.Elapsed.TotalSeconds;
            UpdatePosition((float)Math.Min(now - lastTick, .04));
            lastTick = now;
            Invalidate();
        };
        Shown += delegate { timer.Start(); };
    }

    private static Bitmap Asset(string name)
    {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
        using (var image = Image.FromStream(stream))
            return new Bitmap(image);
    }

    private RectangleF Scene()
    {
        float width = Math.Min(ClientSize.Width - 40, (ClientSize.Height - 106) * GridWidth / (float)GridHeight);
        float height = width * GridHeight / (float)GridWidth;
        return new RectangleF((float)Math.Floor((ClientSize.Width - width) / 2), (float)Math.Floor((ClientSize.Height - height) / 2), width, height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        RectangleF scene = Scene();
        Graphics g = e.Graphics;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        using (Graphics pixels = Graphics.FromImage(frame))
        {
            pixels.InterpolationMode = InterpolationMode.NearestNeighbor;
            pixels.PixelOffsetMode = PixelOffsetMode.Half;
            pixels.DrawImageUnscaled(room, 0, 0);
            if (position.Y < .64f) DrawPlayer(pixels);
            DrawSprite(pixels, companion, new PointF(.56f, .64f), false, 0);
            if (position.Y >= .64f) DrawPlayer(pixels);
        }
        if (scene.Width != GridWidth) g.InterpolationMode = InterpolationMode.HighQualityBilinear;
        g.DrawImage(frame, scene);
        using (var border = new Pen(Color.FromArgb(188, 130, 85), 2))
        using (var brush = new SolidBrush(Cream))
        {
            g.DrawRectangle(border, scene.X, scene.Y, scene.Width, scene.Height);
            g.DrawString(Text, heading, brush, scene.X, scene.Y - 42);
            string controls = "WASD / 方向键移动     E 互动     也可以点击物品";
            SizeF size = g.MeasureString(controls, normal);
            g.DrawString(controls, normal, brush, (ClientSize.Width - size.Width) / 2, scene.Bottom + 15);
            int target = Nearest();
            if (target >= 0)
            {
                string hint = "E  ·  " + labels[target];
                SizeF hintSize = g.MeasureString(hint, normal);
                float x = (ClientSize.Width - hintSize.Width) / 2;
                using (var background = new SolidBrush(BackColor))
                    g.FillRectangle(background, x - 8, scene.Bottom - 42, hintSize.Width + 16, 29);
                g.DrawString(hint, normal, brush, x, scene.Bottom - 39);
            }
        }
    }

    private void DrawPlayer(Graphics g)
    {
        Bitmap image = worn == null ? player : backFacing ? worn.BackImage : sideFacing ? worn.SideImage : worn.Image;
        var state = g.Save();
        g.TranslateTransform(GridWidth * position.X, GridHeight * position.Y);
        if (sideFacing && facing < 0) g.ScaleTransform(-1, 1);
        float bounce = (float)(1 - Math.Cos(phase * 2)) * stride * .7f;
        g.TranslateTransform(-image.Width / 2f + (sideFacing ? 0 : (float)Math.Sin(phase) * stride * .6f), -image.Height - bounce);
        if (worn == null) g.DrawImageUnscaled(image, 0, 0);
        else (backFacing ? worn.BackMotion : sideFacing ? worn.SideMotion : worn.Motion).Draw(g, phase, stride);
        g.Restore(state);
    }

    private static void DrawSprite(Graphics g, Bitmap image, PointF foot, bool flip, int bounce)
    {
        int x = (int)Math.Round(GridWidth * foot.X - image.Width / 2f);
        int y = (int)Math.Round(GridHeight * foot.Y) - image.Height - bounce;
        if (flip)
            g.DrawImage(image, new[] { new Point(x + image.Width,y), new Point(x,y), new Point(x + image.Width,y + image.Height) });
        else
            g.DrawImageUnscaled(image, x, y);
    }
    private static bool Walkable(float x, float y)
    {
        if (x < .075f || x > .91f || y < .405f || y > .96f) return false;
        RectangleF feet = new RectangleF(x - .014f, y - .022f, .028f, .044f);
        foreach (RectangleF obstacle in obstacles)
            if (feet.IntersectsWith(obstacle)) return false;
        return true;
    }

    private void UpdatePosition(float seconds)
    {
        int dx = Axis(Keys.Right, Keys.D) - Axis(Keys.Left, Keys.A);
        int dy = Axis(Keys.Down, Keys.S) - Axis(Keys.Up, Keys.W);
        if (dx != 0) { facing = Math.Sign(dx); sideFacing = true; backFacing = false; }
        else if (dy != 0) { sideFacing = false; backFacing = dy < 0; }
        float step = seconds * .19f / (float)Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
        PointF before = position;
        float x = position.X + dx * step;
        float y = position.Y + dy * step * GridWidth / (float)GridHeight;
        if (Walkable(x, position.Y)) position.X = x;
        if (Walkable(position.X, y)) position.Y = y;
        bool moving = before != position;
        stride += ((moving ? 1f : 0f) - stride) * Math.Min(1f, seconds * 12f);
        if (stride < .001f) { stride = 0; phase = 0; }
        else phase += seconds * 8f;
    }

    private int Axis(Keys primary, Keys alternate)
    {
        return keys.Contains(primary) || keys.Contains(alternate) ? 1 : 0;
    }

    private int Nearest()
    {
        int index = -1;
        float best = .085f * .085f;
        for (int i = 0; i < targets.Length; i++)
        {
            float dx = targets[i].X - position.X;
            float dy = (targets[i].Y - position.Y) * GridHeight / (float)GridWidth;
            float distance = dx * dx + dy * dy;
            if (distance < best) { best = distance; index = i; }
        }
        return index;
    }

    protected override bool IsInputKey(Keys keyData)
    {
        return (keyData >= Keys.Left && keyData <= Keys.Down) || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        bool added = keys.Add(e.KeyCode);
        if (e.KeyCode == Keys.E && added) Interact(Nearest());
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        keys.Remove(e.KeyCode);
        base.OnKeyUp(e);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        RectangleF scene = Scene();
        var point = new PointF((e.X - scene.X) / scene.Width, (e.Y - scene.Y) / scene.Height);
        for (int i = 0; i < hotspots.Length; i++)
            if (hotspots[i].Contains(point)) { Interact(i); break; }
        base.OnMouseClick(e);
    }

    private void Interact(int id)
    {
        if (id < 0) return;
        keys.Clear();
        if (id == 3) { OpenWardrobe(); return; }
        using (var panel = new Form {
            Text = labels[id], ClientSize = new Size(440, id == 0 ? 365 : 155),
            StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false,
            BackColor = Cream, ForeColor = BackColor, Font = normal
        })
        {
            var close = new Button { Text = "返回", Bounds = new Rectangle(340, panel.ClientSize.Height - 44, 80, 30), DialogResult = DialogResult.Cancel };
            panel.CancelButton = close;
            panel.Controls.Add(close);
            var message = new Label { Bounds = new Rectangle(20, id == 0 ? 273 : 25, 400, 56) };
            panel.Controls.Add(message);
            if (id == 0)
            {
                var board = new PictureBox { Image = map, SizeMode = PictureBoxSizeMode.StretchImage, Bounds = new Rectangle(20,15,400,250) };
                panel.Controls.Add(board);
                message.Text = "点地图上的编号查看示例旅行回忆。";
                AddPin(board, 1, 196, 61, message, "示例 · 山间旅行。这里可以放当天的照片和故事。");
                AddPin(board, 2, 300, 82, message, "示例 · 海边旅行。这里可以记录一起看过的风景。");
            }
            else if (id == 1)
            {
                message.Text = "桌上的礼物是给你的。";
                var unwrap = new Button { Text = "拆开礼物", Bounds = new Rectangle(20, 93, 110, 32) };
                unwrap.Click += delegate {
                    message.Text = "生日快乐！愿我们再一起去很多很多地方。";
                    unwrap.Enabled = false;
                };
                panel.Controls.Add(unwrap);
                panel.AcceptButton = unwrap;
            }
            else message.Text = "它摇了摇尾巴，往你手心里蹭了蹭。";
            panel.ShowDialog(this);
        }
    }

    private static void AddPin(Control board, int number, int x, int y, Label label, string text)
    {
        var pin = new Button { Text = number.ToString(), Bounds = new Rectangle(x,y,30,30), BackColor = Cream };
        pin.Click += delegate { label.Text = text; };
        board.Controls.Add(pin);
    }

    private bool SelfTest()
    {
        if (outfits.Count == 0 || worn == null || worn.Image.Height != 177) return false;
        if (room.Width != GridWidth || room.Height != GridHeight || map.Width < 100 || player.Height != 177 || companion.Height != 162) return false;
        keys.Add(Keys.Right);
        for (int i = 0; i < 120; i++) UpdatePosition(.016f);
        keys.Clear();
        bool movement = position.X > .68f && position.X <= .702f && Nearest() == 1;
        if (!sideFacing || facing != 1) return false;
        keys.Add(Keys.Left); UpdatePosition(.016f); keys.Clear();
        if (!sideFacing || facing != -1) return false;
        UpdatePosition(.016f);
        if (!sideFacing) return false; // Idle keeps the last horizontal view.
        keys.Add(Keys.Up); UpdatePosition(.016f); keys.Clear();
        if (!backFacing || sideFacing) return false;
        keys.Add(Keys.Down); UpdatePosition(.016f); keys.Clear();
        return movement && !sideFacing && !backFacing && outfits.TrueForAll(outfit => outfit.SideImage.Height == 177 && outfit.BackImage.Height == 177);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
            foreach (Outfit outfit in outfits)
            {
                outfit.Preview.Dispose();
                outfit.Motion.Dispose();
                outfit.SideMotion.Dispose();
                outfit.SideImage.Dispose();
                outfit.BackMotion.Dispose();
                outfit.BackImage.Dispose();
                outfit.Image.Dispose();
            }
            frame.Dispose();
            room.Dispose();
            map.Dispose();
            player.Dispose();
            companion.Dispose();
            heading.Dispose();
            normal.Dispose();
        }
        base.Dispose(disposing);
    }
}







