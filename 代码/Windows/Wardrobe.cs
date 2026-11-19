using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal sealed partial class Game
{
    private sealed class Outfit
    {
        public string File;
        public string Name;
        public Bitmap Image;
        public Bitmap Preview;
        public WalkMotion Motion;
        public Bitmap SideImage;
        public WalkMotion SideMotion;
    }

    private readonly List<Outfit> outfits = new List<Outfit>();
    private readonly HashSet<string> hiddenOutfits = new HashSet<string>(StringComparer.Ordinal);
    private Outfit worn;
    private static readonly string WardrobeSettings = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BirthdayRoom", "wardrobe.txt");

    private void LoadWardrobe()
    {
        foreach (string resource in Assembly.GetExecutingAssembly().GetManifestResourceNames()
            .Where(name => name.StartsWith("outfit/", StringComparison.Ordinal)).OrderBy(name => name, StringComparer.Ordinal))
        {
            string file = resource.Substring("outfit/".Length);
            Bitmap image = Asset(resource);
            Bitmap side = Asset("outfit-side/" + file);
            outfits.Add(new Outfit {
                File = file, Name = Path.GetFileNameWithoutExtension(file).Substring(3), Image = image,
                Preview = new Bitmap(image, image.Width * 118 / image.Height, 118),
                Motion = new WalkMotion(image, file),
                SideImage = side, SideMotion = new WalkMotion(side, file, true)
            });
        }
        worn = outfits.Find(outfit => outfit.File.StartsWith("05-", StringComparison.Ordinal)) ?? outfits.FirstOrDefault();
        try
        {
            if (!File.Exists(WardrobeSettings)) return;
            string[] settings = File.ReadAllLines(WardrobeSettings);
            if (settings.Length == 0) return;
            worn = outfits.Find(outfit => outfit.File == settings[0]) ?? worn;
            hiddenOutfits.UnionWith(settings.Skip(1));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void SaveWardrobe()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(WardrobeSettings));
            File.WriteAllLines(WardrobeSettings, new[] { worn.File }.Concat(hiddenOutfits));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void OpenWardrobe()
    {
        using (var panel = new Form {
            Text = "LY 的衣橱", ClientSize = new Size(610, 460), MinimumSize = new Size(480, 340),
            StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false,
            BackColor = Cream, ForeColor = BackColor, Font = normal
        })
        {
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 45, Padding = new Padding(6) };
            var grid = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(6) };
            panel.Controls.Add(grid);
            panel.Controls.Add(toolbar);
            RenderWardrobe(toolbar, grid, false);
            panel.ShowDialog(this);
        }
    }

    private void RenderWardrobe(FlowLayoutPanel toolbar, FlowLayoutPanel grid, bool managing)
    {
        ClearControls(toolbar);
        ClearControls(grid);
        var checks = new Dictionary<Outfit, CheckBox>();
        AddWardrobeButton(toolbar, managing ? "返回换装" : "批量管理", delegate { RenderWardrobe(toolbar, grid, !managing); });
        if (managing)
        {
            AddWardrobeButton(toolbar, "全选", delegate {
                foreach (CheckBox check in checks.Values) check.Checked = true;
            });
            AddWardrobeButton(toolbar, "隐藏所选", delegate { ChangeVisibility(checks, true, toolbar, grid); });
            AddWardrobeButton(toolbar, "恢复所选", delegate { ChangeVisibility(checks, false, toolbar, grid); });
        }
        foreach (Outfit outfit in outfits)
        {
            bool hidden = hiddenOutfits.Contains(outfit.File);
            if (!managing && hidden) continue;
            string state = hidden ? " · 已隐藏" : outfit == worn ? " · 穿着中" : "";
            ButtonBase card = managing ? (ButtonBase)new CheckBox { Appearance = Appearance.Button } : new Button();
            card.Size = new Size(132, 155);
            card.Text = outfit.Name + state;
            card.Image = outfit.Preview;
            card.TextImageRelation = TextImageRelation.ImageAboveText;
            card.ImageAlign = ContentAlignment.MiddleCenter;
            card.TextAlign = ContentAlignment.MiddleCenter;
            if (managing) checks.Add(outfit, (CheckBox)card);
            else card.Click += delegate {
                worn = outfit;
                SaveWardrobe();
                Invalidate();
                RenderWardrobe(toolbar, grid, false);
            };
            grid.Controls.Add(card);
        }
        if (grid.Controls.Count == 0)
            grid.Controls.Add(new Label { AutoSize = true, Text = "没有可选装扮，可在批量管理里恢复。" });
    }

    private void ChangeVisibility(Dictionary<Outfit, CheckBox> checks, bool hide, FlowLayoutPanel toolbar, FlowLayoutPanel grid)
    {
        foreach (var item in checks)
        {
            if (!item.Value.Checked) continue;
            if (hide) hiddenOutfits.Add(item.Key.File);
            else hiddenOutfits.Remove(item.Key.File);
        }
        SaveWardrobe();
        RenderWardrobe(toolbar, grid, true);
    }

    private static void AddWardrobeButton(Control parent, string text, EventHandler action)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += action;
        parent.Controls.Add(button);
    }

    private static void ClearControls(Control parent)
    {
        while (parent.Controls.Count > 0) parent.Controls[0].Dispose();
    }
}


