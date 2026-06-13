using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace W3GAnalyzer.UI;

/// <summary>暗色「科技感」主题：调色板 + 字体解析 + 控件样式助手 + 暗色 ToolStrip 渲染器。</summary>
internal static class Theme
{
    // ── 背景层次（由深到浅）──
    public static readonly Color Bg = Color.FromArgb(13, 17, 23);          // #0D1117 最底
    public static readonly Color Surface = Color.FromArgb(22, 27, 34);     // #161B22 面板/标题栏
    public static readonly Color SurfaceAlt = Color.FromArgb(28, 33, 41);  // #1C2129 输入框/交替行
    public static readonly Color Border = Color.FromArgb(48, 54, 61);      // #30363D 边框
    public static readonly Color BorderSubtle = Color.FromArgb(33, 38, 45);

    // ── 文本 ──
    public static readonly Color Text = Color.FromArgb(230, 237, 243);     // #E6EDF3
    public static readonly Color TextMuted = Color.FromArgb(139, 148, 158);// #8B949E

    // ── 强调色（青蓝科技）──
    public static readonly Color Accent = Color.FromArgb(45, 212, 255);    // 霓虹青
    public static readonly Color AccentDim = Color.FromArgb(0, 120, 160);
    public static readonly Color AccentGlow = Color.FromArgb(24, 48, 60);  // 选中行/悬停底色
    public static readonly Color Good = Color.FromArgb(63, 185, 80);       // 绿
    public static readonly Color Bad = Color.FromArgb(248, 81, 73);        // 红
    public static readonly Color Warn = Color.FromArgb(210, 153, 34);      // 黄

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
        g.BackgroundColor = Theme.Bg;
        g.GridColor = Theme.BorderSubtle;
        g.BorderStyle = BorderStyle.None;
        g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        g.ColumnHeadersHeight = 38;
        g.RowTemplate.Height = 30;
        g.RowHeadersVisible = false;
        g.AllowUserToAddRows = false;
        g.AllowUserToDeleteRows = false;
        g.AllowUserToResizeRows = false;
        g.ReadOnly = true;
        g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        g.MultiSelect = false;

        var head = g.ColumnHeadersDefaultCellStyle;
        head.BackColor = Theme.Surface;
        head.ForeColor = Theme.Accent;
        head.SelectionBackColor = Theme.Surface;
        head.SelectionForeColor = Theme.Accent;
        head.Font = Ui(9.5f, FontStyle.Bold);
        head.Alignment = DataGridViewContentAlignment.MiddleLeft;
        head.Padding = new Padding(10, 0, 0, 0);

        var cell = g.DefaultCellStyle;
        cell.BackColor = Theme.Surface;
        cell.ForeColor = Theme.Text;
        cell.SelectionBackColor = Theme.AccentGlow;
        cell.SelectionForeColor = Theme.Accent;
        cell.Font = Ui(9.5f);
        cell.Padding = new Padding(10, 0, 0, 0);

        var alt = g.AlternatingRowsDefaultCellStyle;
        alt.BackColor = Theme.SurfaceAlt;
        alt.ForeColor = Theme.Text;
        alt.SelectionBackColor = Theme.AccentGlow;
        alt.SelectionForeColor = Theme.Accent;
        alt.Padding = new Padding(10, 0, 0, 0);
    }

    public static void StyleReadout(RichTextBox rt)
    {
        rt.Dock = DockStyle.Fill;
        rt.ReadOnly = true;
        rt.BorderStyle = BorderStyle.None;
        rt.BackColor = Theme.Surface;
        rt.ForeColor = Theme.Text;
        rt.Font = Mono(10.5f);
        rt.WordWrap = false;
        rt.ScrollBars = RichTextBoxScrollBars.Both;
        rt.DetectUrls = false;
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

/// <summary>顶部导航的扁平标签按钮，激活时显示青色下划线指示条。</summary>
internal sealed class NavButton : Button
{
    private bool _active;
    public bool Active
    {
        get => _active;
        set { _active = value; Invalidate(); }
    }

    public NavButton(string text)
    {
        Text = text;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        FlatAppearance.MouseOverBackColor = Theme.SurfaceAlt;
        FlatAppearance.MouseDownBackColor = Theme.SurfaceAlt;
        BackColor = Theme.Surface;
        ForeColor = Theme.TextMuted;
        Font = Theme.Ui(10.5f);
        AutoSize = false;
        Height = 44;
        Margin = new Padding(2, 0, 2, 0);
        Cursor = Cursors.Hand;
        TabStop = false;
        int w = TextRenderer.MeasureText(text, Font).Width;
        Width = Math.Max(80, w + 30);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        ForeColor = _active ? Theme.Accent : Theme.TextMuted;
        base.OnPaint(e);
        if (_active)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Theme.Accent, 2.5f);
            int y = Height - 3;
            e.Graphics.DrawLine(pen, 14, y, Width - 14, y);
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

    public override Color MenuItemSelected => Theme.AccentGlow;
    public override Color MenuItemSelectedGradientBegin => Theme.AccentGlow;
    public override Color MenuItemSelectedGradientEnd => Theme.AccentGlow;
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
