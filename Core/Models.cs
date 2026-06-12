namespace W3GAnalyzer.Core;

/// <summary>解析过程中遇到的不可恢复错误。消息里通常带出错偏移。</summary>
public sealed class ReplayParseException : Exception
{
    public ReplayParseException(string message) : base(message) { }
    public ReplayParseException(string message, Exception inner) : base(message, inner) { }
}

public sealed class ReplayHeader
{
    public uint FirstBlockOffset { get; set; }
    public uint CompressedFileSize { get; set; }
    public uint HeaderVersion { get; set; }          // 0 = <=1.06, 1 = >=1.07
    public uint DecompressedSize { get; set; }
    public uint BlockCount { get; set; }

    // SubHeader（仅 HeaderVersion==1）
    public string GameIdentifier { get; set; } = "";  // "W3XP"(TFT) / "WAR3"(RoC)
    public uint VersionNumber { get; set; }           // 如 24 => 1.24；>=10032 重制版
    public ushort BuildNumber { get; set; }
    public ushort Flags { get; set; }                 // 0x8000 多人；国内平台常为 0
    public uint ReplayLengthMs { get; set; }
    public uint HeaderCrc { get; set; }

    public bool IsExpansion => GameIdentifier == "W3XP";
    public bool IsReforged => VersionNumber >= 10032;

    /// <summary>人类可读的版本串，如 "War3 TFT 1.24 (build 6059)"。</summary>
    public string VersionDisplay
    {
        get
        {
            string game = IsExpansion ? "TFT" : "RoC";
            return $"War3 {game} 1.{VersionNumber} (build {BuildNumber})";
        }
    }
}

/// <summary>主机或对局玩家记录（recordId 0x00 / 0x16）。</summary>
public sealed class PlayerRecord
{
    public byte RecordId { get; set; }
    public byte PlayerId { get; set; }
    public string Name { get; set; } = "";
    public byte[] AdditionalData { get; set; } = Array.Empty<byte>();
}

/// <summary>对局开始时的槽位信息（0x19 GameStartRecord 内）。</summary>
public sealed class SlotRecord
{
    public byte PlayerId { get; set; }        // 0 = 电脑
    public byte DownloadPercent { get; set; }
    public byte Status { get; set; }          // 0 空 / 1 关闭 / 2 占用
    public byte ComputerFlag { get; set; }    // 1 = 电脑
    public byte Team { get; set; }            // 12 或 24 = 观察者
    public byte Color { get; set; }
    public byte RaceFlag { get; set; }        // 含 0x40 固定位
    public byte AiStrength { get; set; }
    public byte Handicap { get; set; }

    public bool IsObserver => Team == 12 || Team == 24;
    public bool IsComputer => ComputerFlag == 1;
    public bool IsUsed => Status == 2;
    public int Race => RaceFlag & 0x3F;       // 去掉固定位
}

public sealed class ChatMessage
{
    public uint TimeMs { get; set; }
    public byte PlayerId { get; set; }
    public string PlayerName { get; set; } = "";
    public byte Flags { get; set; }           // 0x10 开场 / 0x20 普通
    public uint Mode { get; set; }            // 0 全体 1 盟友 2 观察者 3+N 私聊
    public string Text { get; set; } = "";

    public string ModeText => Flags == 0x10
        ? "开场"
        : Mode switch
        {
            0 => "全体",
            1 => "盟友",
            2 => "观察者",
            _ => "私聊",
        };
}

public sealed class LeaveEvent
{
    public uint TimeMs { get; set; }
    public uint Reason { get; set; }
    public byte PlayerId { get; set; }
    public string PlayerName { get; set; } = "";
    public uint Result { get; set; }

    public string ReasonText => Reason switch
    {
        0x01 => "远程断开连接",
        0x0C => "本地关闭",
        0x0E => "未知",
        _ => $"0x{Reason:X}",
    };
}

/// <summary>时间线上的一条事件（开局、玩家离开、开场消息、每分钟概要等）。</summary>
public sealed class TimelineEvent
{
    public uint TimeMs { get; set; }
    public string Kind { get; set; } = "";    // 开局/离开/消息/概要
    public string Description { get; set; } = "";
}

/// <summary>合并了槽位信息与动作统计的玩家视图。</summary>
public sealed class PlayerStats
{
    public byte PlayerId { get; set; }
    public string Name { get; set; } = "";
    public int Team { get; set; } = -1;
    public int Color { get; set; } = -1;
    public int Race { get; set; } = -1;       // SlotRecord.Race 值
    public bool IsObserver { get; set; }
    public bool IsComputer { get; set; }

    public long ActionCount { get; set; }     // 计入 APM 的动作数
    public long TotalActions { get; set; }    // 全部已识别动作数
    public uint? LeftAtMs { get; set; }
    public string? LeaveReason { get; set; }
    public uint? Result { get; set; }

    public double Apm { get; set; }

    public string TeamText => IsObserver ? "观察者" : (Team >= 0 ? $"队伍 {Team + 1}" : "-");
    public string ColorText => Lookups.ColorName(Color);
    public string RaceText => Lookups.RaceName(Race);
}

public sealed class GameSettings
{
    public byte Speed { get; set; }           // 0 慢 1 中 2 快
    public byte Visibility { get; set; }
    public byte Observer { get; set; }
    public byte TeamsTogether { get; set; }
    public bool RandomHero { get; set; }
    public bool RandomRaces { get; set; }

    public string SpeedText => Speed switch { 0 => "慢速", 1 => "普通", 2 => "快速", _ => $"({Speed})" };
}

/// <summary>一份录像解析后的完整结果。</summary>
public sealed class ReplaySummary
{
    public string FilePath { get; set; } = "";
    public ReplayHeader Header { get; set; } = new();
    public GameSettings Settings { get; set; } = new();

    public string GameName { get; set; } = "";
    public string MapPath { get; set; } = "";
    public string MapName { get; set; } = "";
    public string GameCreator { get; set; } = "";
    public string HostName { get; set; } = "";

    public uint DurationMs { get; set; }      // max(头部时长, 累计时钟)

    public List<PlayerStats> Players { get; } = new();
    public List<ChatMessage> Chat { get; } = new();
    public List<LeaveEvent> Leaves { get; } = new();
    public List<TimelineEvent> Timeline { get; } = new();
    public List<string> Warnings { get; } = new();

    public string? WinnerGuess { get; set; }

    public int DecompressedBytesConsumed { get; set; }
    public long UnknownActionCount { get; set; }

    public string DurationText => Lookups.FormatTime(DurationMs);
}

public static class Lookups
{
    public static string FormatTime(uint ms)
    {
        uint totalSec = ms / 1000;
        return $"{totalSec / 60:00}:{totalSec % 60:00}";
    }

    public static string ColorName(int c) => c switch
    {
        0 => "红", 1 => "蓝", 2 => "青", 3 => "紫", 4 => "黄", 5 => "橙",
        6 => "绿", 7 => "粉", 8 => "灰", 9 => "浅蓝", 10 => "深绿", 11 => "棕",
        _ => c >= 0 ? c.ToString() : "-",
    };

    public static string RaceName(int r) => r switch
    {
        1 => "人族", 2 => "兽族", 4 => "暗夜", 8 => "不死", 0x20 => "随机",
        _ => r >= 0 ? $"0x{r:X}" : "-",
    };

    /// <summary>从地图路径里取文件名（去目录、去扩展名）。</summary>
    public static string MapNameFromPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        var norm = path.Replace('/', '\\');
        int slash = norm.LastIndexOf('\\');
        var file = slash >= 0 ? norm[(slash + 1)..] : norm;
        int dot = file.LastIndexOf('.');
        return dot > 0 ? file[..dot] : file;
    }
}
