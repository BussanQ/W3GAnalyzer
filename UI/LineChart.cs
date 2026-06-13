using System.Drawing.Drawing2D;

namespace W3GAnalyzer.UI;

/// <summary>暗色折线图：多条按玩家着色的曲线 + 网格 + 坐标轴 + 图例。自绘，无第三方依赖。</summary>
internal sealed class LineChart : Control
{
    public sealed class Series
    {
        public string Name = "";
        public Color Color;
        public IReadOnlyList<int> Values = Array.Empty<int>();
    }

    private List<Series> _series = new();
    public string YUnit = "";
    public string Empty = "暂无数据";

    public LineChart()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Theme.Surface;
        Font = Theme.Ui(9f);
    }

    public void SetSeries(IEnumerable<Series> series)
    {
        _series = series.Where(s => s.Values.Count > 0).ToList();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var plot = new Rectangle(56, 18, Width - 56 - 18, Height - 18 - 64);
        if (plot.Width < 40 || plot.Height < 40) return;

        if (_series.Count == 0)
        {
            using var mb = new SolidBrush(Theme.TextMuted);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(Empty, Theme.Ui(10f), mb, plot, sf);
            return;
        }

        int maxX = Math.Max(1, _series.Max(s => s.Values.Count) - 1);
        int maxY = Math.Max(1, _series.Max(s => s.Values.Count == 0 ? 0 : s.Values.Max()));
        maxY = NiceCeiling(maxY);

        // 网格 + Y 轴刻度
        using (var grid = new Pen(Theme.BorderSubtle))
        using (var axis = new Pen(Theme.Border))
        using (var tb = new SolidBrush(Theme.TextMuted))
        {
            const int yTicks = 4;
            for (int i = 0; i <= yTicks; i++)
            {
                float yy = plot.Bottom - plot.Height * i / (float)yTicks;
                g.DrawLine(grid, plot.Left, yy, plot.Right, yy);
                int val = maxY * i / yTicks;
                g.DrawString(val.ToString(), Font, tb, new RectangleF(0, yy - 8, 50, 16),
                    new StringFormat { Alignment = StringAlignment.Far });
            }
            g.DrawLine(axis, plot.Left, plot.Top, plot.Left, plot.Bottom);
            g.DrawLine(axis, plot.Left, plot.Bottom, plot.Right, plot.Bottom);

            // X 轴刻度（分钟）
            int xStep = Math.Max(1, (int)Math.Ceiling(maxX / 12.0));
            for (int m = 0; m <= maxX; m += xStep)
            {
                float xx = plot.Left + plot.Width * m / (float)maxX;
                g.DrawLine(grid, xx, plot.Top, xx, plot.Bottom);
                g.DrawString(m.ToString(), Font, tb, new RectangleF(xx - 16, plot.Bottom + 4, 32, 16),
                    new StringFormat { Alignment = StringAlignment.Center });
            }
            g.DrawString($"分钟 →   ({YUnit})", Font, tb, plot.Left, plot.Bottom + 22);
        }

        // 曲线
        foreach (var s in _series)
        {
            if (s.Values.Count < 1) continue;
            var pts = new PointF[s.Values.Count];
            for (int i = 0; i < s.Values.Count; i++)
            {
                float xx = plot.Left + plot.Width * i / (float)maxX;
                float yy = plot.Bottom - plot.Height * s.Values[i] / (float)maxY;
                pts[i] = new PointF(xx, yy);
            }
            // 辉光底线
            using (var glow = new Pen(Color.FromArgb(60, s.Color), 5f) { LineJoin = LineJoin.Round })
                if (pts.Length >= 2) g.DrawLines(glow, pts);
            using (var pen = new Pen(s.Color, 2f) { LineJoin = LineJoin.Round })
            {
                if (pts.Length >= 2) g.DrawLines(pen, pts);
                else g.FillEllipse(new SolidBrush(s.Color), pts[0].X - 2, pts[0].Y - 2, 4, 4);
            }
        }

        // 图例
        float lx = plot.Left + 4, ly = plot.Bottom + 40;
        foreach (var s in _series)
        {
            using (var b = new SolidBrush(s.Color)) g.FillRectangle(b, lx, ly + 3, 14, 8);
            lx += 18;
            using var tb = new SolidBrush(Theme.Text);
            string label = s.Name;
            g.DrawString(label, Font, tb, lx, ly);
            lx += g.MeasureString(label, Font).Width + 16;
            if (lx > Width - 100) { lx = plot.Left + 4; ly += 18; }
        }
    }

    private static int NiceCeiling(int v)
    {
        if (v <= 10) return 10;
        int mag = (int)Math.Pow(10, (int)Math.Log10(v));
        int step = mag / 2 == 0 ? 1 : mag / 2;
        return (v / step + 1) * step;
    }
}
