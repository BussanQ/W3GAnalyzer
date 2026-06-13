using W3GAnalyzer.Core;
using W3GAnalyzer.UI;

namespace W3GAnalyzer.Forms;

/// <summary>
/// MainForm 的 RichTextBox 着色/排版助手。从主窗口剥离出来，便于复用与维护。
/// 都是纯函数式渲染（只往传入的 <see cref="RichTextBox"/> 追加带样式文本），
/// 不依赖窗口实例状态。
/// </summary>
public sealed partial class MainForm
{
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
}
