using System.Text;
using W3GAnalyzer.Core;

namespace W3GAnalyzer.Forms;

/// <summary>主窗口：菜单 + 拖放 + 四个标签页（概览/玩家/聊天/时间线）。纯代码布局。</summary>
public sealed class MainForm : Form
{
    private readonly TextBox _overview = new();
    private readonly DataGridView _players = new();
    private readonly DataGridView _chat = new();
    private readonly ListView _timeline = new();
    private readonly StatusStrip _status = new();
    private readonly ToolStripStatusLabel _statusLabel = new();
    private readonly ToolStripMenuItem _exportJson = new("导出 JSON(&J)");
    private readonly ToolStripMenuItem _exportText = new("导出文本(&T)");

    private ReplaySummary? _current;

    public MainForm(string? initialFile = null)
    {
        Text = "魔兽争霸 III 录像分析器";
        Width = 980;
        Height = 680;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9f);
        AllowDrop = true;

        BuildMenu();
        BuildTabs();
        BuildStatus();

        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;

        if (initialFile != null)
            LoadFile(initialFile);
    }

    private void BuildMenu()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("文件(&F)");

        var open = new ToolStripMenuItem("打开(&O)...", null, (_, _) => OpenDialog())
        { ShortcutKeys = Keys.Control | Keys.O };
        _exportJson.Enabled = false;
        _exportJson.Click += (_, _) => Export(json: true);
        _exportText.Enabled = false;
        _exportText.Click += (_, _) => Export(json: false);
        var exit = new ToolStripMenuItem("退出(&X)", null, (_, _) => Close());

        file.DropDownItems.AddRange(new ToolStripItem[]
        {
            open, new ToolStripSeparator(), _exportJson, _exportText,
            new ToolStripSeparator(), exit,
        });

        var help = new ToolStripMenuItem("帮助(&H)");
        help.DropDownItems.Add(new ToolStripMenuItem("关于(&A)", null, (_, _) =>
            MessageBox.Show(this,
                "魔兽争霸 III 录像 (.w3g) 分析器\n" +
                "支持经典版 1.07–1.27 录像\n" +
                "可解析：版本、地图、玩家、聊天、APM、离开事件、时间线\n" +
                "支持拖放打开，可导出 JSON / 文本",
                "关于", MessageBoxButtons.OK, MessageBoxIcon.Information)));

        menu.Items.Add(file);
        menu.Items.Add(help);
        MainMenuStrip = menu;
        Controls.Add(menu);
    }

    private void BuildTabs()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };

        // 概览
        _overview.Multiline = true;
        _overview.ReadOnly = true;
        _overview.ScrollBars = ScrollBars.Both;
        _overview.WordWrap = false;
        _overview.Dock = DockStyle.Fill;
        _overview.Font = new Font("Consolas", 10f);
        _overview.BackColor = Color.White;
        var tabOverview = new TabPage("概览");
        tabOverview.Controls.Add(_overview);

        // 玩家
        ConfigureGrid(_players);
        var tabPlayers = new TabPage("玩家");
        tabPlayers.Controls.Add(_players);

        // 聊天
        ConfigureGrid(_chat);
        var tabChat = new TabPage("聊天");
        tabChat.Controls.Add(_chat);

        // 时间线
        _timeline.Dock = DockStyle.Fill;
        _timeline.View = View.Details;
        _timeline.FullRowSelect = true;
        _timeline.GridLines = true;
        _timeline.Columns.Add("时间", 80);
        _timeline.Columns.Add("类型", 70);
        _timeline.Columns.Add("事件", 760);
        var tabTimeline = new TabPage("时间线");
        tabTimeline.Controls.Add(_timeline);

        tabs.TabPages.AddRange(new[] { tabOverview, tabPlayers, tabChat, tabTimeline });
        Controls.Add(tabs);
        tabs.BringToFront();
    }

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.AllowUserToResizeRows = false;
        grid.BackgroundColor = Color.White;
    }

    private void BuildStatus()
    {
        _statusLabel.Text = "拖入或打开一个 .w3g 文件";
        _status.Items.Add(_statusLabel);
        _status.Dock = DockStyle.Bottom;
        Controls.Add(_status);
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
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            LoadFile(files[0]);
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
            _statusLabel.Text = $"{Path.GetFileName(path)} · 解析 {sw.ElapsedMilliseconds} ms · " +
                                $"警告 {_current.Warnings.Count}";
            Text = $"{Path.GetFileName(path)} - 魔兽争霸 III 录像分析器";
        }
        catch (Exception ex)
        {
            sw.Stop();
            MessageBox.Show(this, ex.Message, "解析失败",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            _statusLabel.Text = $"解析失败：{ex.Message}";
        }
    }

    private void Populate(ReplaySummary s)
    {
        // 概览
        var sb = new StringBuilder();
        sb.AppendLine($"文件      : {s.FilePath}");
        sb.AppendLine($"游戏版本  : {s.Header.VersionDisplay}");
        sb.AppendLine($"标识      : {s.Header.GameIdentifier}  flags=0x{s.Header.Flags:X4}");
        sb.AppendLine($"地图      : {s.MapName}");
        sb.AppendLine($"地图路径  : {s.MapPath}");
        sb.AppendLine($"游戏名    : {s.GameName}");
        sb.AppendLine($"主机      : {s.HostName}");
        sb.AppendLine($"创建者    : {s.GameCreator}");
        sb.AppendLine($"时长      : {s.DurationText}  ({s.DurationMs} ms)");
        sb.AppendLine($"游戏速度  : {s.Settings.SpeedText}");
        sb.AppendLine($"随机英雄  : {(s.Settings.RandomHero ? "是" : "否")}   随机种族: {(s.Settings.RandomRaces ? "是" : "否")}");
        sb.AppendLine($"玩家数    : {s.Players.Count}");
        if (s.WinnerGuess != null) sb.AppendLine($"推测胜方  : {s.WinnerGuess}");
        if (s.Warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("── 警告 ──");
            foreach (var w in s.Warnings) sb.AppendLine($"  · {w}");
        }
        _overview.Text = sb.ToString().Replace("\n", "\r\n");

        // 玩家
        _players.Columns.Clear();
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

        // 聊天
        _chat.Columns.Clear();
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

        // 时间线
        _timeline.Items.Clear();
        foreach (var t in s.Timeline)
        {
            var item = new ListViewItem(Lookups.FormatTime(t.TimeMs));
            item.SubItems.Add(t.Kind);
            item.SubItems.Add(t.Description);
            _timeline.Items.Add(item);
        }
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
            _statusLabel.Text = $"已导出：{dlg.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "导出失败",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
