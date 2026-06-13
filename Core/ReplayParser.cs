using System.IO.Compression;
using System.Text;

namespace W3GAnalyzer.Core;

/// <summary>
/// 把一份 .w3g 文件解析成 <see cref="ReplaySummary"/>。流程：
/// 主头 → 子头 → 解压数据块 → 静态对局信息 → 回放事件流 → 汇总。
/// 设计为容错优先：回放流中途出错时返回已得到的部分结果并记 warning，
/// 而不是整体失败。
/// </summary>
public sealed class ReplayParser
{
    private static readonly byte[] Magic =
        Encoding.ASCII.GetBytes("Warcraft III recorded game\x1A");  // 27 字节，第 28 字节是 0x00

    public static ReplaySummary Parse(string path)
    {
        TextDecoder.EnsureProviderRegistered();
        var bytes = File.ReadAllBytes(path);
        var result = new ReplaySummary { FilePath = path };

        ParseMainHeader(bytes, result);
        if (result.Header.HeaderVersion == 0)
            throw new ReplayParseException("过旧的录像版本（patch ≤ 1.06），暂不支持解析。");
        ParseSubHeader(bytes, result);
        if (result.Header.IsReforged)
            throw new ReplayParseException(
                $"重制版录像（版本 {result.Header.VersionNumber}）暂不支持解析。");

        byte[] data = Decompress(bytes, result);
        int streamStart = ParseStaticData(data, result);
        ParseReplayStream(data, streamStart, result);
        Aggregate(result);

        return result;
    }

    // ── 主头（48 字节 @ 0）──────────────────────────────────────────────
    private static void ParseMainHeader(byte[] bytes, ReplaySummary result)
    {
        if (bytes.Length < 0x40)
            throw new ReplayParseException("文件过小，不是有效的 w3g 录像。");

        for (int i = 0; i < Magic.Length; i++)
        {
            if (bytes[i] != Magic[i])
                throw new ReplayParseException("文件头魔数不匹配，不是魔兽争霸 III 录像文件。");
        }
        if (bytes[27] != 0x00)
            throw new ReplayParseException("文件头终止符缺失，文件可能已损坏。");

        var r = new BinaryReaderEx(bytes, 0x1C);
        var h = result.Header;
        h.FirstBlockOffset = r.ReadUInt32();   // 0x1C
        h.CompressedFileSize = r.ReadUInt32(); // 0x20
        h.HeaderVersion = r.ReadUInt32();      // 0x24
        h.DecompressedSize = r.ReadUInt32();   // 0x28
        h.BlockCount = r.ReadUInt32();         // 0x2C
    }

    // ── 子头（20 字节 @ 0x30，仅 HeaderVersion==1）─────────────────────
    private static void ParseSubHeader(byte[] bytes, ReplaySummary result)
    {
        var r = new BinaryReaderEx(bytes, 0x30);
        var h = result.Header;

        var idBytes = r.ReadBytes(4);          // 倒序存储
        Array.Reverse(idBytes);
        h.GameIdentifier = Encoding.ASCII.GetString(idBytes);

        h.VersionNumber = r.ReadUInt32();
        h.BuildNumber = r.ReadUInt16();
        h.Flags = r.ReadUInt16();
        h.ReplayLengthMs = r.ReadUInt32();
        h.HeaderCrc = r.ReadUInt32();
    }

    // ── 解压所有数据块 ─────────────────────────────────────────────────
    private static byte[] Decompress(byte[] bytes, ReplaySummary result)
    {
        var h = result.Header;
        var r = new BinaryReaderEx(bytes, (int)h.FirstBlockOffset);
        using var outStream = new MemoryStream((int)h.DecompressedSize);

        for (uint i = 0; i < h.BlockCount; i++)
        {
            if (r.Remaining < 8) { result.Warnings.Add($"第 {i} 个数据块头缺失，提前结束解压。"); break; }

            int compSize = r.ReadUInt16();
            r.ReadUInt16();   // 解压后大小（通常 8192），按总大小截断，无需依赖
            r.ReadUInt32();   // 校验和，忽略

            if (compSize <= 0 || compSize > r.Remaining)
            {
                result.Warnings.Add($"第 {i} 个数据块大小异常（{compSize}），提前结束解压。");
                break;
            }

            byte[] comp = r.ReadBytes(compSize);
            try
            {
                using var ms = new MemoryStream(comp);
                using var zs = new ZLibStream(ms, CompressionMode.Decompress);
                zs.CopyTo(outStream);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"第 {i} 个数据块解压失败（{ex.Message}），使用已解压部分继续。");
                break;
            }
        }

        byte[] data = outStream.ToArray();
        if (data.Length > h.DecompressedSize)
        {
            // 末块零填充，按头部声明的大小截断。
            var trimmed = new byte[h.DecompressedSize];
            Array.Copy(data, trimmed, trimmed.Length);
            data = trimmed;
        }
        else if (data.Length < h.DecompressedSize)
        {
            result.Warnings.Add(
                $"解压数据不完整：得到 {data.Length} 字节，头部声明 {h.DecompressedSize} 字节。");
        }
        return data;
    }

    // ── 静态对局信息 ───────────────────────────────────────────────────
    /// <summary>解析静态信息，返回回放事件流在解压缓冲区中的起始偏移。</summary>
    private static int ParseStaticData(byte[] data, ReplaySummary result)
    {
        var r = new BinaryReaderEx(data);
        r.Skip(4); // 4 未知字节

        // 主机记录
        var host = ReadPlayerRecord(r);
        result.HostName = host.Name;
        var players = new Dictionary<byte, PlayerRecord> { [host.PlayerId] = host };

        result.GameName = r.ReadCString();
        r.ReadByte(); // 1 个 0x00

        // 编码设置字符串
        byte[] encodedRaw = r.ReadCStringRaw();
        byte[] decoded = EncodedStringDecoder.Decode(encodedRaw);
        ParseGameSettings(decoded, result);

        r.ReadUInt32();              // 玩家/槽位数（用 GameStartRecord 的更准）
        r.ReadUInt32();              // game type
        r.ReadUInt32();              // language id

        // 其余玩家记录
        while (r.Peek() == 0x16)
        {
            var p = ReadPlayerRecord(r);
            players[p.PlayerId] = p;
            r.ReadUInt32();          // 4 未知字节（0）
        }

        // GameStartRecord
        var slots = new List<SlotRecord>();
        if (r.Peek() == 0x19)
        {
            r.ReadByte();                       // 0x19
            int dataSize = r.ReadUInt16();
            int nSlots = r.ReadByte();
            int slotBytes = nSlots > 0 ? (dataSize - 7) / nSlots : 9;
            if (slotBytes < 7) slotBytes = 9;   // 防御：异常时退回常见值

            for (int i = 0; i < nSlots; i++)
            {
                if (r.Remaining < slotBytes) break;
                int before = r.Position;
                var s = new SlotRecord
                {
                    PlayerId = r.ReadByte(),
                    DownloadPercent = r.ReadByte(),
                    Status = r.ReadByte(),
                    ComputerFlag = r.ReadByte(),
                    Team = r.ReadByte(),
                    Color = r.ReadByte(),
                    RaceFlag = r.ReadByte(),
                };
                if (slotBytes >= 8) s.AiStrength = r.ReadByte();
                if (slotBytes >= 9) s.Handicap = r.ReadByte();
                // 跳过该槽位剩余的多余字节
                int consumed = r.Position - before;
                if (consumed < slotBytes) r.Skip(slotBytes - consumed);
                slots.Add(s);
            }

            r.ReadUInt32();   // random seed
            r.ReadByte();     // select mode
            r.ReadByte();     // start spot count
        }
        else
        {
            result.Warnings.Add("未找到 GameStartRecord(0x19)，槽位信息可能缺失。");
        }

        BuildPlayers(result, players, slots);
        // 回放流从这里开始
        result.DecompressedBytesConsumed = data.Length;
        return r.Position;
    }

    private static PlayerRecord ReadPlayerRecord(BinaryReaderEx r)
    {
        var p = new PlayerRecord
        {
            RecordId = r.ReadByte(),
            PlayerId = r.ReadByte(),
            Name = r.ReadCString(),
        };
        int addSize = r.ReadByte();
        p.AdditionalData = r.ReadBytes(addSize);
        return p;
    }

    private static void ParseGameSettings(byte[] decoded, ReplaySummary result)
    {
        var s = new GameSettings();
        var r = new BinaryReaderEx(decoded);
        try
        {
            s.Speed = r.ReadByte();
            s.Visibility = r.ReadByte();
            s.TeamsTogether = r.ReadByte();
            byte shared = r.ReadByte();
            s.RandomHero = (shared & 0x02) != 0;
            s.RandomRaces = (shared & 0x04) != 0;

            r.Skip(5);                       // 0x04-0x08：未知（5 字节）
            result.MapChecksum = r.ReadUInt32(); // 0x09-0x0C：地图校验和（4 字节）
            result.MapPath = r.ReadCString();
            result.MapName = Lookups.MapNameFromPath(result.MapPath);
            result.GameCreator = r.ReadCString();
            // 其后还有一个空字符串，忽略
        }
        catch (ReplayParseException)
        {
            result.Warnings.Add("游戏设置字符串解析不完整。");
        }
        result.Settings = s;
    }

    private static void BuildPlayers(ReplaySummary result,
                                     Dictionary<byte, PlayerRecord> players,
                                     List<SlotRecord> slots)
    {
        foreach (var slot in slots)
        {
            if (slot.Status != 2 && !slot.IsComputer) continue; // 仅占用的槽位
            var ps = new PlayerStats
            {
                PlayerId = slot.PlayerId,
                Team = slot.Team,
                Color = slot.Color,
                Race = slot.Race,
                IsObserver = slot.IsObserver,
                IsComputer = slot.IsComputer,
            };
            ps.Name = slot.IsComputer
                ? $"电脑({Lookups.ColorName(slot.Color)})"
                : (players.TryGetValue(slot.PlayerId, out var rec) ? rec.Name : $"玩家{slot.PlayerId}");
            result.Players.Add(ps);
        }

        // 兜底：若槽位里没拿到任何玩家，至少把玩家记录列出来
        if (result.Players.Count == 0)
        {
            foreach (var p in players.Values)
                result.Players.Add(new PlayerStats { PlayerId = p.PlayerId, Name = p.Name });
        }
    }

    // ── 回放事件流 ─────────────────────────────────────────────────────
    private static void ParseReplayStream(byte[] data, int streamStart, ReplaySummary result)
    {
        var r = new BinaryReaderEx(data, streamStart);
        uint timeMs = 0;
        // 电脑/空槽位都用 PlayerId 0，可能重复——用索引器赋值避免冲突。
        var byId = new Dictionary<byte, PlayerStats>();
        foreach (var p in result.Players) byId[p.PlayerId] = p;

        string NameOf(byte id) => byId.TryGetValue(id, out var p) ? p.Name : $"玩家{id}";

        try
        {
            while (!r.Eof)
            {
                byte block = r.ReadByte();
                switch (block)
                {
                    case 0x00:
                        // 末尾填充
                        goto done;

                    case 0x17: // LeaveGame
                    {
                        uint reason = r.ReadUInt32();
                        byte pid = r.ReadByte();
                        uint res = r.ReadUInt32();
                        r.ReadUInt32(); // unknown
                        var le = new LeaveEvent
                        {
                            TimeMs = timeMs, Reason = reason, PlayerId = pid,
                            PlayerName = NameOf(pid), Result = res,
                        };
                        result.Leaves.Add(le);
                        if (byId.TryGetValue(pid, out var ps))
                        {
                            ps.LeftAtMs = timeMs;
                            ps.LeaveReason = le.ReasonText;
                            ps.Result = res;
                        }
                        break;
                    }

                    case 0x1A: case 0x1B: case 0x1C:
                        r.Skip(4);
                        break;

                    case 0x1E: case 0x1F: // TimeSlot
                    {
                        int n = r.ReadUInt16();
                        if (n >= 2)
                        {
                            ushort inc = r.ReadUInt16();
                            timeMs += inc;
                            int cmdLen = n - 2;
                            byte[] cmd = r.ReadBytes(cmdLen);
                            ParseCommandData(cmd, byId, result);
                        }
                        // n < 2：空时间片，无增量
                        break;
                    }

                    case 0x20: // ChatMessage
                    {
                        byte pid = r.ReadByte();
                        int byteCount = r.ReadUInt16();
                        byte[] payload = r.ReadBytes(byteCount);
                        ParseChat(payload, pid, timeMs, NameOf(pid), result);
                        break;
                    }

                    case 0x22:
                    {
                        int len = r.ReadByte();
                        r.Skip(len);
                        break;
                    }

                    case 0x23:
                        r.Skip(10);
                        break;

                    case 0x2F:
                        r.Skip(8);
                        break;

                    default:
                        result.Warnings.Add(
                            $"回放流中遇到未知块 0x{block:X2} @ 0x{r.Position - 1:X}，返回部分结果。");
                        goto done;
                }
            }
        }
        catch (ReplayParseException ex)
        {
            result.Warnings.Add($"回放流解析中断：{ex.Message}");
        }

    done:
        result.DurationMs = Math.Max(result.Header.ReplayLengthMs, timeMs);

        // 一致性检查（仅警告）
        if (result.Header.ReplayLengthMs > 0)
        {
            double diff = Math.Abs((double)timeMs - result.Header.ReplayLengthMs)
                          / result.Header.ReplayLengthMs;
            if (diff > 0.05)
                result.Warnings.Add(
                    $"累计游戏时钟({Lookups.FormatTime(timeMs)})与头部时长" +
                    $"({Lookups.FormatTime(result.Header.ReplayLengthMs)})偏差 {diff:P0}。");
        }
    }

    private static void ParseCommandData(byte[] cmd,
                                         Dictionary<byte, PlayerStats> byId,
                                         ReplaySummary result)
    {
        var r = new BinaryReaderEx(cmd);
        while (r.Remaining >= 3)
        {
            byte pid = r.ReadByte();
            int len = r.ReadUInt16();
            if (len > r.Remaining)
            {
                result.UnknownActionCount++;
                break;
            }
            byte[] slice = r.ReadBytes(len);
            if (byId.TryGetValue(pid, out var ps))
                result.UnknownActionCount += ActionParser.Parse(slice, ps);
        }
    }

    private static void ParseChat(byte[] payload, byte pid, uint timeMs,
                                  string name, ReplaySummary result)
    {
        var r = new BinaryReaderEx(payload);
        try
        {
            byte flags = r.ReadByte();
            uint mode = 0;
            if (flags == 0x20)
                mode = r.ReadUInt32();
            string text = r.ReadCString();

            var msg = new ChatMessage
            {
                TimeMs = timeMs, PlayerId = pid, PlayerName = name,
                Flags = flags, Mode = mode, Text = text,
            };
            result.Chat.Add(msg);
        }
        catch (ReplayParseException)
        {
            // 单条聊天解析失败不影响整体
        }
    }

    // ── 汇总：APM、时间线、胜方推测 ────────────────────────────────────
    private static void Aggregate(ReplaySummary result)
    {
        double minutes = result.DurationMs / 60000.0;
        if (minutes <= 0) minutes = 1;

        foreach (var p in result.Players)
            p.Apm = Math.Round(p.ActionCount / minutes, 1);

        // 构建时间线：开局 + 全部聊天 + 全部离开事件，按时间排序
        result.Timeline.Add(new TimelineEvent
        {
            TimeMs = 0, Kind = "开局",
            Description = $"对局开始 · 地图 {result.MapName} · {result.Players.Count} 名玩家",
        });
        foreach (var c in result.Chat)
            result.Timeline.Add(new TimelineEvent
            {
                TimeMs = c.TimeMs, Kind = "聊天",
                Description = $"({c.ModeText}) {c.PlayerName}：{c.Text}",
            });
        foreach (var l in result.Leaves)
            result.Timeline.Add(new TimelineEvent
            {
                TimeMs = l.TimeMs, Kind = "离开",
                Description = $"{l.PlayerName} 离开（{l.ReasonText}）",
            });
        result.Timeline.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));

        // 胜方推测：最后离开的非观察者玩家所在队伍（败方通常先离开）
        var contenders = result.Leaves
            .Where(l => result.Players.Any(p => p.PlayerId == l.PlayerId && !p.IsObserver))
            .ToList();
        if (contenders.Count > 0)
        {
            var last = contenders[^1];
            var lp = result.Players.First(p => p.PlayerId == last.PlayerId);
            result.WinnerGuess = lp.IsObserver
                ? null
                : $"{lp.TeamText}（最后离开：{last.PlayerName}，推测）";
        }
    }
}
