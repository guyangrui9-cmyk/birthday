using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;
internal static class PrepareSideSprites
{
    static void Main(string[] args)
    {
        string[] files = Directory.GetFiles(args[1], "*.png"); Array.Sort(files);
        Directory.CreateDirectory(args[2]);
        using (var atlas = new Bitmap(args[0]))
        for (int i = 0; i < files.Length; i++)
        {
            int col = i % 7, row = i / 7;
            int x0 = col * atlas.Width / 7, x1 = (col+1) * atlas.Width / 7;
            int y0 = row * atlas.Height / 3, y1 = (row+1) * atlas.Height / 3;
            int left=x1, right=x0, top=y1, bottom=y0;
            // A neighboring dress can protrude into a cell. Follow the main
            // connected silhouette rather than including that detached fragment.
            var visited = new HashSet<int>();
            int largest = 0;
            for (int sy=y0; sy<y1; sy++) for (int sx=x0; sx<x1; sx++)
            {
                int seed=sy*atlas.Width+sx;
                if (visited.Contains(seed) || atlas.GetPixel(sx,sy).A <= 180) continue;
                var queue=new Queue<Point>(); queue.Enqueue(new Point(sx,sy)); visited.Add(seed);
                int count=0, l=sx, r=sx;
                while(queue.Count>0)
                {
                    Point p=queue.Dequeue(); count++; l=Math.Min(l,p.X);r=Math.Max(r,p.X);
                    for(int dy=-1;dy<=1;dy++) for(int dx=-1;dx<=1;dx++)
                    {
                        int x=p.X+dx,y=p.Y+dy;
                        if(x<x0 || x>=x1 || y<y0 || y>=y1) continue;
                        int key=y*atlas.Width+x;
                        if(!visited.Contains(key) && atlas.GetPixel(x,y).A>180)
                        {visited.Add(key);queue.Enqueue(new Point(x,y));}
                    }
                }
                if(count>largest){largest=count;left=l;right=r;}
            }
            for (int y=y0; y<y1; y++) for (int x=left; x<=right; x++)
                if (atlas.GetPixel(x,y).A > 180)
                { top=Math.Min(top,y);bottom=Math.Max(bottom,y); }
            if (left>=right || top>=bottom) throw new Exception("Empty atlas cell " + i);
            var crop=new Rectangle(left,top,right-left+1,bottom-top+1);
            int width=(int)Math.Round(crop.Width*177.0/crop.Height);
            using(var sprite=new Bitmap(width,177,PixelFormat.Format32bppArgb))
            using(var g=Graphics.FromImage(sprite))
            {
                g.InterpolationMode=InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode=PixelOffsetMode.Half;
                g.DrawImage(atlas,new Rectangle(0,0,width,177),crop,GraphicsUnit.Pixel);
                sprite.Save(Path.Combine(args[2],Path.GetFileName(files[i])),ImageFormat.Png);
            }
            Console.WriteLine(Path.GetFileName(files[i])+" "+width+"x177");
        }
    }
}
