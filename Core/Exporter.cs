using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace W3GAnalyzer.Core;

/// <summary>把 <see cref="ReplaySummary"/> 导出为 JSON 或纯文本。</summary>
public static class Exporter
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        // 否则中文会被转义成 \uXXXX
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string ToJson(ReplaySummary s)
    {
        var dto = new
        {
            file = s.FilePath,
            header = new
            {
                game = s.Header.GameIdentifier,
                isExpansion = s.Header.IsExpansion,
                version = s.Header.VersionNumber,
                versionDisplay = s.Header.VersionDisplay,
                build = s.Header.BuildNumber,
                flags = s.Header.Flags,
                durationMs = s.DurationMs,
                duration = s.DurationText,
            },
            map = new { path = s.MapPath, name = s.MapName, checksum = $"0x{s.MapChecksum:X8}" },
            gameName = s.GameName,
            gameCreator = s.GameCreator,
            host = s.HostName,
            settings = new
            {
                speed = s.Settings.SpeedText,
                randomHero = s.Settings.RandomHero,
                randomRaces = s.Settings.RandomRaces,
            },
            players = s.Players.Select(p => new
            {
                id = p.PlayerId,
                name = p.Name,
                team = p.TeamText,
                color = p.ColorText,
                race = p.RaceText,
                isObserver = p.IsObserver,
                isComputer = p.IsComputer,
                apm = p.Apm,
                actions = p.TotalActions,
                apmActions = p.ActionCount,
                leftAtMs = p.LeftAtMs,
                leaveReason = p.LeaveReason,
                result = p.Result,
            }),
            chat = s.Chat.Select(c => new
            {
                timeMs = c.TimeMs,
                time = Lookups.FormatTime(c.TimeMs),
                player = c.PlayerName,
                mode = c.ModeText,
                text = c.Text,
            }),
            leaves = s.Leaves.Select(l => new
            {
                timeMs = l.TimeMs,
                time = Lookups.FormatTime(l.TimeMs),
                player = l.PlayerName,
                reason = l.ReasonText,
                result = l.Result,
            }),
            timeline = s.Timeline.Select(t => new
            {
                timeMs = t.TimeMs,
                time = Lookups.FormatTime(t.TimeMs),
                kind = t.Kind,
                description = t.Description,
            }),
            winnerGuess = s.WinnerGuess,
            warnings = s.Warnings,
        };
        return JsonSerializer.Serialize(dto, JsonOpts);
    }

    public static string ToText(ReplaySummary s)
    {
        var sb = new StringBuilder();
        sb.AppendLine("══════════ 魔兽争霸 III 录像分析 ══════════");
        sb.AppendLine($"文件      : {s.FilePath}");
        sb.AppendLine($"游戏版本  : {s.Header.VersionDisplay}");
        sb.AppendLine($"地图      : {s.MapName}  ({s.MapPath})");
        sb.AppendLine($"游戏名    : {s.GameName}");
        sb.AppendLine($"主机      : {s.HostName}");
        sb.AppendLine($"创建者    : {s.GameCreator}");
        sb.AppendLine($"时长      : {s.DurationText}");
        sb.AppendLine($"游戏速度  : {s.Settings.SpeedText}");
        sb.AppendLine($"玩家数    : {s.Players.Count}");
        if (s.WinnerGuess != null) sb.AppendLine($"推测胜方  : {s.WinnerGuess}");
        sb.AppendLine();

        sb.AppendLine("── 玩家 ──");
        sb.AppendLine($"{"ID",-3} {"玩家名",-18} {"队伍",-8} {"颜色",-6} {"种族",-6} {"APM",6} {"动作",8} 离开");
        foreach (var p in s.Players)
        {
            string left = p.LeftAtMs.HasValue ? $"{Lookups.FormatTime(p.LeftAtMs.Value)} {p.LeaveReason}" : "-";
            sb.AppendLine($"{p.PlayerId,-3} {Pad(p.Name, 18)} {Pad(p.TeamText, 8)} {Pad(p.ColorText, 6)} " +
                          $"{Pad(p.RaceText, 6)} {p.Apm,6} {p.TotalActions,8} {left}");
        }
        sb.AppendLine();

        sb.AppendLine("── 聊天 ──");
        foreach (var c in s.Chat)
            sb.AppendLine($"[{Lookups.FormatTime(c.TimeMs)}] ({c.ModeText}) {c.PlayerName}: {c.Text}");
        sb.AppendLine();

        if (s.Warnings.Count > 0)
        {
            sb.AppendLine("── 警告 ──");
            foreach (var w in s.Warnings) sb.AppendLine($"  · {w}");
        }
        return sb.ToString();
    }

    // 中文按宽度 2 估算的简单对齐
    private static string Pad(string text, int width)
    {
        int w = 0;
        foreach (var ch in text) w += ch > 0x7F ? 2 : 1;
        int pad = width - w;
        return pad > 0 ? text + new string(' ', pad) : text;
    }
}
