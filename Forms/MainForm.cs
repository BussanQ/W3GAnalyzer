using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;
using W3GAnalyzer.Core;
using W3GAnalyzer.UI;

namespace W3GAnalyzer.Forms;

/// <summary>主窗口：暗色科技风。自绘标题头 + 扁平导航标签 + 五个视图（概览/玩家/聊天/时间线/地图对比）。</summary>
public sealed class MainForm : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private readonly RichTextBox _overview = new();
    private readonly DataGridView _players = new();
    private readonly DataGridView _chat = new();
    private readonly DataGridView _timeline = new();
    private readonly RichTextBox _mapCompare = new();

    private readonly StatusStrip _status = new();
    private readonly ToolStripStatusLabel _statusLabel = new();
    private readonly ToolStripStatusLabel _statusDot = new();
    private readonly ToolStripMenuItem _exportJson = new("导出 JSON(&J)");
    private readonly ToolStripMenuItem _exportText = new("导出文本(&T)");
    private readonly ToolStripMenuItem _compareMap = new("对比地图(&M)...");

    private readonly Control[] _views;
    private readonly NavButton[] _navs;
    private const int ViewOverview = 0, ViewPlayers = 1, ViewChat = 2, ViewTimeline = 3, ViewMap = 4;

    private ReplaySummary? _current;

    public MainForm(string? initialFile = null)
    {
        Text = "魔兽争霸 III 录像分析器";
        Width = 1180;
        Height = 760;
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        Font = Theme.Ui(9f);
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        AllowDrop = true;
        LoadAppIcon();

        _views = new Control[] { _overview, _players, _chat, _timeline, _mapCompare };
        _navs = new[]
        {
            new NavButton("概览"), new NavButton("玩家"), new NavButton("聊天"),
            new NavButton("时间线"), new NavButton("地图对比"),
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Bg,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86)); // header
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); // nav
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // content
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // status

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildNav(), 0, 1);
        root.Controls.Add(BuildContent(), 0, 2);
        root.Controls.Add(BuildStatus(), 0, 3);

        Controls.Add(root);
        Controls.Add(BuildMenu()); // 菜单最后加入 → 停靠在最顶

        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;

        ShowView(ViewOverview);
        ShowEmptyState();

        if (initialFile != null)
            LoadFile(initialFile);
    }

    private void LoadAppIcon()
    {
        try
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            using var s = asm.GetManifestResourceStream("W3GAnalyzer.Assets.app.ico");
            if (s != null) Icon = new Icon(s);
        }
        catch { /* 图标缺失不影响运行 */ }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // 暗色标题栏（Win10 1809+ 用 19，Win10 2004+/Win11 用 20）
        int on = 1;
        if (DwmSetWindowAttribute(Handle, 20, ref on, sizeof(int)) != 0)
            DwmSetWindowAttribute(Handle, 19, ref on, sizeof(int));
    }

    // ── 菜单栏 ──
    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip
        {
            Renderer = new DarkRenderer(),
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            Padding = new Padding(6, 2, 0, 2),
        };

        var file = new ToolStripMenuItem("文件(&F)") { ForeColor = Theme.Text };
        var open = new ToolStripMenuItem("打开录像(&O)...", null, (_, _) => OpenDialog())
        { ShortcutKeys = Keys.Control | Keys.O, ForeColor = Theme.Text };

        _exportJson.Enabled = false;
        _exportJson.ForeColor = Theme.Text;
        _exportJson.Click += (_, _) => Export(json: true);
        _exportText.Enabled = false;
        _exportText.ForeColor = Theme.Text;
        _exportText.Click += (_, _) => Export(json: false);
        _compareMap.Enabled = false;
        _compareMap.ForeColor = Theme.Text;
        _compareMap.Click += (_, _) => ChooseAndCompareMap();

        var exit = new ToolStripMenuItem("退出(&X)", null, (_, _) => Close()) { ForeColor = Theme.Text };
        file.DropDownItems.AddRange(new ToolStripItem[]
        {
            open, _compareMap, new ToolStripSeparator(), _exportJson, _exportText,
            new ToolStripSeparator(), exit,
        });

        var help = new ToolStripMenuItem("帮助(&H)") { ForeColor = Theme.Text };
        help.DropDownItems.Add(new ToolStripMenuItem("关于(&A)", null, (_, _) =>
            MessageBox.Show(this,
                "魔兽争霸 III 录像 (.w3g) 分析器\n" +
                "支持经典版 1.07–1.27 录像\n" +
                "可解析：版本、地图、玩家、聊天、APM、离开事件、时间线\n" +
                "支持拖放打开，可导出 JSON / 文本\n" +
                "加载录像后拖入 .w3x 地图可对比是否一致（按文件 SHA-256 指纹）",
                "关于", MessageBoxButtons.OK, MessageBoxIcon.Information))
        { ForeColor = Theme.Text });

        menu.Items.Add(file);
        menu.Items.Add(help);
        MainMenuStrip = menu;
        return menu;
    }

    // ── 自绘标题头：徽标 + 字标 + 底部强调线 ──
    private DBPanel BuildHeader()
    {
        var header = new DBPanel { Dock = DockStyle.Fill, BackColor = Theme.Surface };
        header.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var bg = new Rectangle(0, 0, header.Width, header.Height);
            using (var grad = new LinearGradientBrush(bg, Theme.SurfaceLift, Theme.Surface, 0f))
                g.FillRectangle(grad, bg);

            var logo = new Rectangle(22, 20, 46, 46);
            using (var path = Theme.RoundRect(logo, 8))
            using (var grad = new LinearGradientBrush(logo, Theme.Accent, Theme.Amber, 45f))
                g.FillPath(grad, path);
            using (var lf = Theme.Ui(15f, FontStyle.Bold))
            {
                var sz = g.MeasureString("W3", lf);
                using var b = new SolidBrush(Color.FromArgb(14, 18, 24));
                g.DrawString("W3", lf, b,
                    logo.Left + (logo.Width - sz.Width) / 2, logo.Top + (logo.Height - sz.Height) / 2);
            }

            using (var wf = Theme.Ui(18f, FontStyle.Bold))
            using (var b = new SolidBrush(Theme.Text))
                g.DrawString("W3G Analyzer", wf, b, 84, 17);
            using (var sf = Theme.Ui(9.5f))
            using (var b = new SolidBrush(Theme.TextMuted))
                g.DrawString("魔兽争霸 III 录像解析、玩家统计、聊天记录与地图指纹比对", sf, b, 86, 47);

            string hint = "拖放 .w3g 录像或 .w3x/.w3m 地图";
            using (var hf = Theme.Ui(9f, FontStyle.Bold))
            {
                var sz = g.MeasureString(hint, hf);
                var pill = new Rectangle(
                    Math.Max(20, header.Width - (int)sz.Width - 42),
                    27,
                    (int)sz.Width + 24,
                    32);
                using (var path = Theme.RoundRect(pill, 8))
                using (var fill = new SolidBrush(Theme.Bg))
                using (var pen = new Pen(Theme.BorderSubtle))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(pen, path);
                }
                using var hb = new SolidBrush(Theme.TextMuted);
                g.DrawString(hint, hf, hb, pill.Left + 12, pill.Top + 7);
            }

            int y = header.Height - 1;
            using (var p = new Pen(Theme.Border)) g.DrawLine(p, 0, y, header.Width, y);
            using (var grad = new LinearGradientBrush(
                new Rectangle(22, y - 2, Math.Max(1, header.Width - 44), 2), Theme.Accent, Theme.Amber, 0f))
            using (var p = new Pen(grad, 2f))
                g.DrawLine(p, 22, y, Math.Min(header.Width - 22, 430), y);
        };
        return header;
    }

    // ── 导航标签 ──
    private DBPanel BuildNav()
    {
        var bar = new DBPanel { Dock = DockStyle.Fill, BackColor = Theme.Surface };
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            Padding = new Padding(16, 0, 0, 0),
            WrapContents = false,
        };
        for (int i = 0; i < _navs.Length; i++)
        {
            int idx = i;
            _navs[i].Click += (_, _) => ShowView(idx);
            flow.Controls.Add(_navs[i]);
        }
        bar.Controls.Add(flow);
        bar.Paint += (_, e) =>
        {
            int y = bar.Height - 1;
            using var p = new Pen(Theme.Border);
            e.Graphics.DrawLine(p, 0, y, bar.Width, y);
        };
        return bar;
    }

    // ── 内容区：五视图叠放，按需切换可见 ──
    private DBPanel BuildContent()
    {
        var host = new DBPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Bg,
            Padding = new Padding(18, 16, 18, 16),
        };
        host.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.BorderSubtle);
            e.Graphics.DrawRectangle(pen, 17, 15, Math.Max(1, host.Width - 35), Math.Max(1, host.Height - 33));
        };

        Theme.StyleReadout(_overview);
        Theme.StyleReadout(_mapCompare);

        Theme.StyleGrid(_players);
        Theme.StyleGrid(_chat);

        Theme.StyleGrid(_timeline);
        _timeline.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        foreach (var v in _views)
        {
            v.Dock = DockStyle.Fill;
            v.Visible = false;
            v.BackColor = Theme.Surface;
            host.Controls.Add(v);
        }
        return host;
    }

    // ── 状态栏 ──
    private StatusStrip BuildStatus()
    {
        _statusDot.Text = "●";
        _statusDot.ForeColor = Theme.TextMuted;
        _statusDot.Font = Theme.Ui(9f);
        _statusLabel.Text = "等待载入 · 拖入或打开一个 .w3g 文件";
        _statusLabel.ForeColor = Theme.TextMuted;

        _status.Renderer = new DarkRenderer();
        _status.BackColor = Theme.Surface;
        _status.Dock = DockStyle.Fill;
        _status.SizingGrip = false;
        _status.Padding = new Padding(12, 3, 0, 0);
        _status.Items.Add(_statusDot);
        _status.Items.Add(_statusLabel);
        return _status;
    }

    private void SetStatus(string text, Color dot)
    {
        _statusDot.ForeColor = dot;
        _statusLabel.Text = text;
    }

    private void ShowView(int index)
    {
        for (int i = 0; i < _views.Length; i++)
        {
            _views[i].Visible = i == index;
            _navs[i].Active = i == index;
        }
        _views[index].BringToFront();
    }

    private void OpenDialog()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "魔兽争霸III录像 (*.w3g)|*.w3g|所有文件 (*.*)|*.*",
            Title = "打开录像文件",
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            LoadFile(dlg.FileName);
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return;
        string path = files[0];
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".w3x" or ".w3m")
            CompareMap(path);
        else
            LoadFile(path);
    }

    private void ChooseAndCompareMap()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "魔兽争霸III地图 (*.w3x;*.w3m)|*.w3x;*.w3m|所有文件 (*.*)|*.*",
            Title = "选择要对比的地图文件",
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            CompareMap(dlg.FileName);
    }

    private void CompareMap(string mapPath)
    {
        if (_current == null)
        {
            MessageBox.Show(this, "请先加载一个录像文件，再拖入地图对比。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            var r = MapFingerprint.Compare(_current, mapPath);
            Color vc = r.FingerprintMatch == true ? Theme.Good
                     : r.FingerprintMatch == false ? Theme.Bad
                     : Theme.Warn;

            _mapCompare.Clear();
            Section(_mapCompare, "对比结论");
            _mapCompare.SelectionFont = Theme.Mono(11f, FontStyle.Bold);
            _mapCompare.SelectionColor = vc;
            _mapCompare.AppendText("  " + r.Verdict + "\n");

            Section(_mapCompare, "录像所用地图");
            Kv(_mapCompare, "地图名", r.ReplayMapName);
            Kv(_mapCompare, "相对路径", r.ReplayMapPath);
            Kv(_mapCompare, "内置校验和", $"0x{r.ReplayChecksum:X8}", Theme.TextMuted);
            Hint(_mapCompare, "（含 common.j/blizzard.j，跨补丁不可复现，仅供参考）");

            Section(_mapCompare, "拖入的地图");
            Kv(_mapCompare, "文件", r.DraggedPath);
            Kv(_mapCompare, "大小", $"{r.DraggedSize:N0} 字节");
            Kv(_mapCompare, "SHA-256", r.DraggedSha, Theme.Accent);
            Kv(_mapCompare, "文件名匹配", r.NameMatch ? "是" : "否", r.NameMatch ? Theme.Good : Theme.Bad);

            if (r.ReferencePath != null)
            {
                Section(_mapCompare, "本机定位到的参照地图");
                Kv(_mapCompare, "文件", r.ReferencePath);
                Kv(_mapCompare, "大小", $"{r.ReferenceSize:N0} 字节");
                Kv(_mapCompare, "SHA-256", r.ReferenceSha, Theme.Accent);
                Kv(_mapCompare, "内容一致", r.FingerprintMatch == true ? "是（完全相同）" : "否（同名不同内容）",
                    r.FingerprintMatch == true ? Theme.Good : Theme.Bad);
            }
            else
            {
                Section(_mapCompare, "参照地图");
                Hint(_mapCompare, "  未能在录像路径对应位置定位到本机地图文件，只能做文件名级别比对。");
            }
            _mapCompare.SelectionStart = 0;
            _mapCompare.ScrollToCaret();

            ShowView(ViewMap);
            SetStatus($"地图对比 · {Path.GetFileName(mapPath)} — {r.Verdict}", vc);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "地图对比失败",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadFile(string path)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _current = ReplayParser.Parse(path);
            sw.Stop();
            Populate(_current);
            _exportJson.Enabled = true;
            _exportText.Enabled = true;
            _compareMap.Enabled = true;
            ShowView(ViewOverview);
            var dot = _current.Warnings.Count > 0 ? Theme.Warn : Theme.Good;
            SetStatus($"{Path.GetFileName(path)} · 解析 {sw.ElapsedMilliseconds} ms · 警告 {_current.Warnings.Count}", dot);
            Text = $"{Path.GetFileName(path)} — 魔兽争霸 III 录像分析器";
        }
        catch (Exception ex)
        {
            sw.Stop();
            MessageBox.Show(this, ex.Message, "解析失败",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus($"解析失败：{ex.Message}", Theme.Bad);
        }
    }

    private void Populate(ReplaySummary s)
    {
        // ── 概览 ──
        _overview.Clear();
        OverviewTitle(_overview, s);
        Section(_overview, "关键指标");
        Kv(_overview, "时长", s.DurationText, Theme.Accent);
        Kv(_overview, "玩家", $"{s.Players.Count} 名");
        Kv(_overview, "聊天", $"{s.Chat.Count} 条");
        Kv(_overview, "时间线", $"{s.Timeline.Count} 条事件");
        Kv(_overview, "警告", $"{s.Warnings.Count} 条", s.Warnings.Count > 0 ? Theme.Warn : Theme.Good);

        Section(_overview, "录像");
        Kv(_overview, "文件", s.FilePath);
        Kv(_overview, "游戏版本", s.Header.VersionDisplay, Theme.Accent);
        Kv(_overview, "标识", $"{s.Header.GameIdentifier}   flags=0x{s.Header.Flags:X4}");

        Section(_overview, "地图与对局");
        Kv(_overview, "地图", s.MapName, Theme.Accent);
        Kv(_overview, "地图路径", s.MapPath);
        Kv(_overview, "地图校验和", $"0x{s.MapChecksum:X8}", Theme.TextMuted);
        Kv(_overview, "游戏名", s.GameName);
        Kv(_overview, "主机", s.HostName);
        Kv(_overview, "创建者", s.GameCreator);
        Kv(_overview, "时长", $"{s.DurationText}  ({s.DurationMs} ms)", Theme.Accent);
        Kv(_overview, "游戏速度", s.Settings.SpeedText);
        Kv(_overview, "随机英雄", s.Settings.RandomHero ? "是" : "否");
        Kv(_overview, "随机种族", s.Settings.RandomRaces ? "是" : "否");
        Kv(_overview, "玩家数", s.Players.Count.ToString());
        if (s.WinnerGuess != null)
            Kv(_overview, "推测胜方", s.WinnerGuess, Theme.Good);

        if (s.Warnings.Count > 0)
        {
            Section(_overview, "警告");
            foreach (var w in s.Warnings)
            {
                _overview.SelectionColor = Theme.Warn;
                _overview.AppendText("  · " + w + "\n");
            }
        }
        _overview.SelectionStart = 0;
        _overview.ScrollToCaret();

        // ── 玩家 ──
        _players.DataSource = s.Players.Select(p => new
        {
            ID = p.PlayerId,
            玩家名 = p.Name,
            队伍 = p.TeamText,
            颜色 = p.ColorText,
            种族 = p.RaceText,
            APM = p.Apm,
            动作数 = p.TotalActions,
            离开时间 = p.LeftAtMs.HasValue ? Lookups.FormatTime(p.LeftAtMs.Value) : "-",
            离开原因 = p.LeaveReason ?? "-",
        }).ToList();

        // ── 聊天 ──
        _chat.DataSource = s.Chat.Select(c => new
        {
            时间 = Lookups.FormatTime(c.TimeMs),
            玩家 = c.PlayerName,
            频道 = c.ModeText,
            内容 = c.Text,
        }).ToList();
        if (_chat.Columns.Count > 0)
        {
            _chat.Columns["时间"].FillWeight = 12;
            _chat.Columns["玩家"].FillWeight = 20;
            _chat.Columns["频道"].FillWeight = 12;
            _chat.Columns["内容"].FillWeight = 56;
        }

        // ── 时间线 ──
        _timeline.DataSource = s.Timeline.Select(t => new
        {
            时间 = Lookups.FormatTime(t.TimeMs),
            类型 = t.Kind,
            事件 = t.Description,
        }).ToList();
        if (_timeline.Columns.Count > 0)
        {
            _timeline.Columns["时间"].FillWeight = 12;
            _timeline.Columns["类型"].FillWeight = 14;
            _timeline.Columns["事件"].FillWeight = 74;
        }
    }

    private void ShowEmptyState()
    {
        _overview.Clear();
        _overview.SelectionColor = Theme.TextMuted;
        _overview.SelectionFont = Theme.Ui(10f);
        _overview.AppendText("\n\n\n");
        _overview.SelectionColor = Theme.Accent;
        _overview.SelectionFont = Theme.Ui(18f, FontStyle.Bold);
        _overview.AppendText("   尚未载入录像\n\n");
        _overview.SelectionColor = Theme.TextMuted;
        _overview.SelectionFont = Theme.Ui(10.5f);
        _overview.AppendText(
            "   把 .w3g 录像文件拖入本窗口，或用「文件 → 打开录像」。\n" +
            "   载入后可在上方标签查看玩家、聊天、时间线，并可继续拖入地图做 SHA-256 指纹比对。\n");

        _mapCompare.Clear();
        _mapCompare.SelectionColor = Theme.TextMuted;
        _mapCompare.SelectionFont = Theme.Ui(10.5f);
        _mapCompare.AppendText(
            "\n   先加载一个录像，然后把地图文件 (*.w3x/*.w3m) 拖到本窗口，\n" +
            "   或用「文件 → 对比地图」选择地图，校验它是否与录像所用地图一致。\n");
    }

    // ── RichTextBox 着色助手 ──
    private static void OverviewTitle(RichTextBox rt, ReplaySummary s)
    {
        rt.SelectionColor = Theme.Text;
        rt.SelectionFont = Theme.Ui(16f, FontStyle.Bold);
        rt.AppendText("  " + (string.IsNullOrWhiteSpace(s.GameName) ? "已载入录像" : s.GameName) + "\n");
        rt.SelectionColor = Theme.TextMuted;
        rt.SelectionFont = Theme.Ui(9.5f);
        rt.AppendText($"  {Path.GetFileName(s.FilePath)}  ·  {s.Header.VersionDisplay}  ·  {s.MapName}\n");
    }

    private static void Section(RichTextBox rt, string title)
    {
        rt.SelectionColor = Theme.Amber;
        rt.SelectionFont = Theme.Ui(10.5f, FontStyle.Bold);
        rt.AppendText("\n  " + title + "\n");
    }

    private static void Kv(RichTextBox rt, string key, string value, Color? valueColor = null)
    {
        rt.SelectionColor = Theme.TextMuted;
        rt.SelectionFont = Theme.Mono(10f);
        rt.AppendText("    " + PadWidth(key, 12));
        rt.SelectionColor = valueColor ?? Theme.Text;
        rt.AppendText(value + "\n");
    }

    private static void Hint(RichTextBox rt, string text)
    {
        rt.SelectionColor = Theme.TextMuted;
        rt.SelectionFont = Theme.Mono(9f);
        rt.AppendText("    " + new string(' ', 12) + text + "\n");
    }

    /// <summary>按显示宽度右侧补空格（中文按 2 宽计）。</summary>
    private static string PadWidth(string s, int width)
    {
        int w = 0;
        foreach (var ch in s) w += ch > 0x7F ? 2 : 1;
        return w < width ? s + new string(' ', width - w) : s + " ";
    }

    private void Export(bool json)
    {
        if (_current == null) return;
        using var dlg = new SaveFileDialog
        {
            Filter = json ? "JSON 文件 (*.json)|*.json" : "文本文件 (*.txt)|*.txt",
            FileName = Path.GetFileNameWithoutExtension(_current.FilePath) + (json ? ".json" : ".txt"),
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            string content = json ? Exporter.ToJson(_current) : Exporter.ToText(_current);
            File.WriteAllText(dlg.FileName, content, new UTF8Encoding(false));
            SetStatus($"已导出：{dlg.FileName}", Theme.Good);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "导出失败",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
