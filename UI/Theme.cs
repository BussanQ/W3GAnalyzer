using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace W3GAnalyzer.UI;

/// <summary>暗色分析工具主题：调色板、字体解析、控件样式助手和 ToolStrip 渲染器。</summary>
internal static class Theme
{
    public static readonly Color Bg = Color.FromArgb(10, 13, 18);
    public static readonly Color Surface = Color.FromArgb(24, 29, 38);
    public static readonly Color SurfaceAlt = Color.FromArgb(31, 37, 48);
    public static readonly Color SurfaceLift = Color.FromArgb(38, 45, 58);
    public static readonly Color Border = Color.FromArgb(58, 66, 80);
    public static readonly Color BorderSubtle = Color.FromArgb(42, 49, 62);

    public static readonly Color Text = Color.FromArgb(238, 242, 247);
    public static readonly Color TextMuted = Color.FromArgb(151, 163, 179);

    public static readonly Color Accent = Color.FromArgb(90, 184, 255);
    public static readonly Color AccentDim = Color.FromArgb(24, 98, 138);
    public static readonly Color AccentSoft = Color.FromArgb(32, 65, 92);
    public static readonly Color Amber = Color.FromArgb(247, 181, 78);
    public static readonly Color Good = Color.FromArgb(71, 191, 121);
    public static readonly Color Bad = Color.FromArgb(239, 91, 88);
    public static readonly Color Warn = Color.FromArgb(229, 170, 73);

    private static readonly HashSet<string> Installed =
        new InstalledFontCollection().Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly string MonoFamily =
        new[] { "Cascadia Mono", "Cascadia Code", "JetBrains Mono", "Consolas" }
            .FirstOrDefault(Installed.Contains) ?? "Consolas";

    private static readonly string UiFamily =
        new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" }
            .FirstOrDefault(Installed.Contains) ?? "Segoe UI";

    public static Font Mono(float size, FontStyle style = FontStyle.Regular) => new(MonoFamily, size, style);
    public static Font Ui(float size, FontStyle style = FontStyle.Regular) => new(UiFamily, size, style);

    // ── 控件样式助手 ──

    public static void StyleGrid(DataGridView g)
    {
        g.EnableHeadersVisualStyles = false;
        g.BackgroundColor = Bg;
        g.GridColor = Theme.BorderSubtle;
        g.BorderStyle = BorderStyle.None;
        g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        g.ColumnHeadersHeight = 42;
        g.RowTemplate.Height = 34;
        g.RowHeadersVisible = false;
        g.AllowUserToAddRows = false;
        g.AllowUserToDeleteRows = false;
        g.AllowUserToResizeRows = false;
        g.ReadOnly = true;
        g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        g.MultiSelect = false;
        g.Dock = DockStyle.Fill;
        g.Margin = Padding.Empty;
        g.BackgroundColor = Surface;

        var head = g.ColumnHeadersDefaultCellStyle;
        head.BackColor = SurfaceAlt;
        head.ForeColor = Text;
        head.SelectionBackColor = SurfaceAlt;
        head.SelectionForeColor = Text;
        head.Font = Ui(9.5f, FontStyle.Bold);
        head.Alignment = DataGridViewContentAlignment.MiddleLeft;
        head.Padding = new Padding(12, 0, 0, 0);

        var cell = g.DefaultCellStyle;
        cell.BackColor = Surface;
        cell.ForeColor = Text;
        cell.SelectionBackColor = AccentSoft;
        cell.SelectionForeColor = Text;
        cell.Font = Ui(9.5f);
        cell.Padding = new Padding(12, 0, 8, 0);

        var alt = g.AlternatingRowsDefaultCellStyle;
        alt.BackColor = SurfaceAlt;
        alt.ForeColor = Text;
        alt.SelectionBackColor = AccentSoft;
        alt.SelectionForeColor = Text;
        alt.Padding = new Padding(12, 0, 8, 0);
    }

    public static void StyleReadout(RichTextBox rt)
    {
        rt.Dock = DockStyle.Fill;
        rt.ReadOnly = true;
        rt.BorderStyle = BorderStyle.None;
        rt.BackColor = Surface;
        rt.ForeColor = Text;
        rt.Font = Mono(10.5f);
        rt.WordWrap = false;
        rt.ScrollBars = RichTextBoxScrollBars.Both;
        rt.DetectUrls = false;
        rt.Margin = Padding.Empty;
    }

    /// <summary>画一个圆角矩形路径。</summary>
    public static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        int d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

/// <summary>双缓冲面板，自绘背景不闪烁。</summary>
internal class DBPanel : Panel
{
    public DBPanel() { DoubleBuffered = true; ResizeRedraw = true; }
}

/// <summary>顶部导航按钮，激活时使用柔和填充和短强调线。</summary>
internal sealed class NavButton : Button
{
    private bool _active;
    public bool Active
    {
        get => _active;
        set
        {
            _active = value;
            ForeColor = _active ? Theme.Accent : Theme.TextMuted;
            BackColor = _active ? Theme.AccentSoft : Theme.Surface;
            Invalidate();
        }
    }

    public NavButton(string text)
    {
        Text = text;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        FlatAppearance.MouseOverBackColor = Theme.SurfaceLift;
        FlatAppearance.MouseDownBackColor = Theme.AccentSoft;
        BackColor = Theme.Surface;
        ForeColor = Theme.TextMuted;
        Font = Theme.Ui(10f, FontStyle.Bold);
        AutoSize = false;
        Height = 40;
        Margin = new Padding(4, 6, 4, 6);
        Cursor = Cursors.Hand;
        TabStop = false;
        int w = TextRenderer.MeasureText(text, Font).Width;
        Width = Math.Max(84, w + 34);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_active)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Theme.Accent, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            int y = Height - 5;
            e.Graphics.DrawLine(pen, 18, y, Width - 18, y);
        }
    }
}

/// <summary>暗色 ToolStrip 配色（菜单栏/状态栏）。</summary>
internal sealed class DarkColorTable : ProfessionalColorTable
{
    public DarkColorTable() => UseSystemColors = false;

    public override Color MenuStripGradientBegin => Theme.Surface;
    public override Color MenuStripGradientEnd => Theme.Surface;
    public override Color ToolStripGradientBegin => Theme.Surface;
    public override Color ToolStripGradientMiddle => Theme.Surface;
    public override Color ToolStripGradientEnd => Theme.Surface;
    public override Color ToolStripContentPanelGradientBegin => Theme.Surface;
    public override Color ToolStripContentPanelGradientEnd => Theme.Surface;
    public override Color StatusStripGradientBegin => Theme.Surface;
    public override Color StatusStripGradientEnd => Theme.Surface;

    public override Color MenuItemSelected => Theme.AccentSoft;
    public override Color MenuItemSelectedGradientBegin => Theme.AccentSoft;
    public override Color MenuItemSelectedGradientEnd => Theme.AccentSoft;
    public override Color MenuItemPressedGradientBegin => Theme.SurfaceAlt;
    public override Color MenuItemPressedGradientMiddle => Theme.SurfaceAlt;
    public override Color MenuItemPressedGradientEnd => Theme.SurfaceAlt;
    public override Color MenuItemBorder => Theme.Accent;
    public override Color MenuBorder => Theme.Border;

    public override Color ToolStripDropDownBackground => Theme.Surface;
    public override Color ImageMarginGradientBegin => Theme.Surface;
    public override Color ImageMarginGradientMiddle => Theme.Surface;
    public override Color ImageMarginGradientEnd => Theme.Surface;

    public override Color SeparatorDark => Theme.Border;
    public override Color SeparatorLight => Theme.Border;
}

/// <summary>使用暗色配色并强制亮色文字的渲染器。</summary>
internal sealed class DarkRenderer : ToolStripProfessionalRenderer
{
    public DarkRenderer() : base(new DarkColorTable()) { RoundedEdges = false; }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled
            ? (e.Item.Selected ? Theme.Accent : Theme.Text)
            : Theme.TextMuted;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Theme.TextMuted;
        base.OnRenderArrow(e);
    }
}
