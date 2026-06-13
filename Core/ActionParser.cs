namespace W3GAnalyzer.Core;

/// <summary>
/// 解析一个玩家 CommandData 切片里的动作序列。切片由 TimeSlot 的 u16 长度
/// 限定，因此即便某个动作长度估错也只影响本切片，不会跨块扩散。识别失败时
/// 放弃本切片剩余部分并计入 UnknownActionCount。
///
/// 动作长度表对应 patch >=1.13（覆盖 1.21–1.24 时代）。
/// </summary>
public static class ActionParser
{
    // 计入 APM 的动作（w3gjs / php-parser 惯例）：下达命令、选择/编队、子组、
    // 出队列、ESC、进入技能/建造子菜单。暂停/变速/存档/同步/作弊不计。
    private static readonly HashSet<byte> ApmActions = new()
    {
        0x10, 0x11, 0x12, 0x13, 0x14, 0x16, 0x17, 0x18, 0x19, 0x1E, 0x61, 0x66, 0x67,
    };

    /// <summary>
    /// 解析单个玩家的动作切片，更新该玩家的统计（含按分钟分桶的 APM、英雄/技能/建造提取）。
    /// <paramref name="timeMs"/> 是本切片所在 TimeSlot 的游戏时钟。返回本切片内未识别到的动作数。
    /// </summary>
    public static long Parse(byte[] slice, PlayerStats stats, uint timeMs)
    {
        var r = new BinaryReaderEx(slice);
        long unknown = 0;

        while (!r.Eof)
        {
            byte action = r.ReadByte();

            // 0x10/0x11/0x12/0x14：能力/训练/建造/施法——读出 object-id 做深度提取。
            if (action is 0x10 or 0x11 or 0x12 or 0x14)
            {
                int total = action switch { 0x10 => 14, 0x11 => 22, 0x12 => 30, _ => 43 };
                if (total > r.Remaining) { unknown++; break; }
                r.ReadUInt16();                  // ability flags
                string? code = ReadObjectId(r);  // 消费 4 字节
                r.Skip(total - 6);

                stats.TotalActions++;
                CountApm(stats, timeMs);         // 这几个都计入 APM
                if (code != null) Record(stats, code, timeMs);
                continue;
            }

            int len;
            try
            {
                len = ActionLength(action, r);
            }
            catch (ReplayParseException)
            {
                // 越界（变长动作读取失败）——本切片到此为止。
                unknown++;
                break;
            }

            if (len < 0)
            {
                // 未知动作 id：无法确定长度，安全起见放弃本切片剩余部分。
                unknown++;
                break;
            }

            if (len > r.Remaining)
            {
                // 长度超出切片——估算有误，停止本切片。
                unknown++;
                break;
            }

            r.Skip(len);

            stats.TotalActions++;
            if (ApmActions.Contains(action))
                CountApm(stats, timeMs);
        }

        return unknown;
    }

    private static void CountApm(PlayerStats stats, uint timeMs)
    {
        stats.ActionCount++;
        int minute = (int)(timeMs / 60000);
        while (stats.PerMinuteApm.Count <= minute) stats.PerMinuteApm.Add(0);
        stats.PerMinuteApm[minute]++;
    }

    private static void Record(PlayerStats stats, string code, uint timeMs)
    {
        switch (ObjectData.Categorize(code))
        {
            case ObjCat.Hero:
                stats.Heroes[code] = stats.Heroes.GetValueOrDefault(code) + 1;
                if (!stats.HeroFirstMs.ContainsKey(code)) stats.HeroFirstMs[code] = timeMs;
                break;
            case ObjCat.Ability:
                stats.Abilities[code] = stats.Abilities.GetValueOrDefault(code) + 1;
                break;
            case ObjCat.Build:
            case ObjCat.Upgrade:
            case ObjCat.Item:
                stats.Builds[code] = stats.Builds.GetValueOrDefault(code) + 1;
                break;
        }
    }

    /// <summary>读 4 字节 object-id。可打印的 4 字符编码按小端反转还原（如 "Hpal"）；
    /// 否则是预定义指令号（移动/右键等），返回 null。</summary>
    private static string? ReadObjectId(BinaryReaderEx r)
    {
        byte b0 = r.ReadByte(), b1 = r.ReadByte(), b2 = r.ReadByte(), b3 = r.ReadByte();
        if (IsLetter(b3) && IsAlnum(b2) && IsAlnum(b1) && IsPrintable(b0))
            return new string(new[] { (char)b3, (char)b2, (char)b1, (char)b0 });
        return null;
    }

    private static bool IsLetter(byte b) => (b >= 'A' && b <= 'Z') || (b >= 'a' && b <= 'z');
    private static bool IsAlnum(byte b) => IsLetter(b) || (b >= '0' && b <= '9');
    private static bool IsPrintable(byte b) => b >= 0x20 && b < 0x7F;

    /// <summary>
    /// 返回 action id 后续负载的字节数；变长动作会就地从 reader 消费掉负载并返回 0。
    /// 未知 id 返回 -1。
    /// </summary>
    private static int ActionLength(byte action, BinaryReaderEx r)
    {
        switch (action)
        {
            // 暂停/变速类
            case 0x01: return 0;   // pause
            case 0x02: return 0;   // resume
            case 0x03: return 1;   // set speed
            case 0x04: return 0;   // increase speed
            case 0x05: return 0;   // decrease speed

            case 0x06:             // save game：跟一个 C 字符串
                r.ReadCStringRaw();
                return 0;
            case 0x07: return 4;   // save finished

            // 单位/建筑命令
            case 0x10: return 14;  // ability，无目标
            case 0x11: return 22;  // ability + 目标坐标
            case 0x12: return 30;  // ability + 坐标 + 目标对象
            case 0x13: return 38;  // give/drop item
            case 0x14: return 43;  // ability + 两组目标

            case 0x16:             // change selection
            {
                r.ReadByte();             // select mode
                int n = r.ReadUInt16();
                return n * 8;
            }
            case 0x17:             // assign group hotkey
            {
                r.ReadByte();             // group number
                int n = r.ReadUInt16();
                return n * 8;
            }
            case 0x18: return 2;   // select group hotkey
            case 0x19: return 12;  // select subgroup (>=1.14b)
            case 0x1A: return 0;   // pre subselect
            case 0x1B: return 9;   // unknown
            case 0x1C: return 9;   // select ground item
            case 0x1D: return 8;   // cancel hero revival
            case 0x1E: return 5;   // remove unit from build queue
            case 0x21: return 8;   // unknown

            // 作弊码：大多 0 字节，少数有负载
            case 0x20: return 0;
            case 0x22: return 0;
            case 0x23: return 0;
            case 0x24: return 0;
            case 0x25: return 0;
            case 0x26: return 0;
            case 0x27: return 5;
            case 0x28: return 5;
            case 0x29: return 0;
            case 0x2A: return 0;
            case 0x2B: return 0;
            case 0x2C: return 0;
            case 0x2D: return 5;
            case 0x2E: return 4;
            case 0x2F: return 0;
            case 0x30: return 0;
            case 0x31: return 0;
            case 0x32: return 0;

            case 0x50: return 5;   // change ally options
            case 0x51: return 9;   // transfer resources

            case 0x60:             // map trigger chat command
                r.Skip(8);
                r.ReadCStringRaw();
                return 0;
            case 0x61: return 0;   // ESC pressed
            case 0x62: return 12;  // scenario trigger
            case 0x66: return 0;   // enter hero skill submenu
            case 0x67: return 0;   // enter building submenu
            case 0x68: return 12;  // minimap signal (ping)
            case 0x69: return 16;  // continue game (block B)
            case 0x6A: return 16;  // continue game (block A)
            case 0x75: return 1;   // unknown

            default: return -1;    // 未知 id
        }
    }
}
