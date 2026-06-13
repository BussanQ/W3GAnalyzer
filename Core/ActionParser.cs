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
    /// <summary>
    /// 单个动作的元数据。把原先散落三处的信息（长度、是否计 APM、是否需深度提取
    /// object-id）合并为一张声明式表，新增动作只需在 <see cref="Actions"/> 加一行。
    /// </summary>
    /// <param name="FixedLen">定长动作的负载字节数（<see cref="Read"/> 为 null 时使用）。</param>
    /// <param name="Read">变长动作：就地从 reader 消费负载，返回还需跳过的字节数。</param>
    /// <param name="CountsApm">是否计入 APM（取代旧的 ApmActions 集合）。</param>
    /// <param name="ObjectIdLen">&gt;0 表示需读出 object-id 做深度提取，值为动作总长度。</param>
    private readonly record struct ActionDef(
        int FixedLen,
        Func<BinaryReaderEx, int>? Read,
        bool CountsApm,
        int ObjectIdLen);

    private static ActionDef Fixed(int len, bool apm = false) => new(len, null, apm, 0);
    private static ActionDef Var(Func<BinaryReaderEx, int> read, bool apm = false) => new(0, read, apm, 0);
    private static ActionDef Obj(int total) => new(0, null, true, total);

    // 动作长度表对应 patch >=1.13。计入 APM 的动作遵循 w3gjs / php-parser 惯例：
    // 下达命令、选择/编队、子组、出队列、ESC、进入技能/建造子菜单。
    // 暂停/变速/存档/同步/作弊不计。未列入的 id 视为未知，放弃本切片剩余部分。
    private static readonly Dictionary<byte, ActionDef> Actions = new()
    {
        // 暂停/变速类
        [0x01] = Fixed(0),   // pause
        [0x02] = Fixed(0),   // resume
        [0x03] = Fixed(1),   // set speed
        [0x04] = Fixed(0),   // increase speed
        [0x05] = Fixed(0),   // decrease speed
        [0x06] = Var(r => { r.ReadCStringRaw(); return 0; }),  // save game：跟一个 C 字符串
        [0x07] = Fixed(4),   // save finished

        // 单位/建筑命令（0x10/0x11/0x12/0x14 深度提取 object-id，均计 APM）
        [0x10] = Obj(14),               // ability，无目标
        [0x11] = Obj(22),               // ability + 目标坐标
        [0x12] = Obj(30),               // ability + 坐标 + 目标对象
        [0x13] = Fixed(38, apm: true),  // give/drop item
        [0x14] = Obj(43),               // ability + 两组目标

        [0x16] = Var(r => { r.ReadByte(); return r.ReadUInt16() * 8; }, apm: true),  // change selection
        [0x17] = Var(r => { r.ReadByte(); return r.ReadUInt16() * 8; }, apm: true),  // assign group hotkey
        [0x18] = Fixed(2, apm: true),   // select group hotkey
        [0x19] = Fixed(12, apm: true),  // select subgroup (>=1.14b)
        [0x1A] = Fixed(0),   // pre subselect
        [0x1B] = Fixed(9),   // unknown
        [0x1C] = Fixed(9),   // select ground item
        [0x1D] = Fixed(8),   // cancel hero revival
        [0x1E] = Fixed(5, apm: true),   // remove unit from build queue
        [0x21] = Fixed(8),   // unknown

        // 作弊码：大多 0 字节，少数有负载
        [0x20] = Fixed(0),
        [0x22] = Fixed(0),
        [0x23] = Fixed(0),
        [0x24] = Fixed(0),
        [0x25] = Fixed(0),
        [0x26] = Fixed(0),
        [0x27] = Fixed(5),
        [0x28] = Fixed(5),
        [0x29] = Fixed(0),
        [0x2A] = Fixed(0),
        [0x2B] = Fixed(0),
        [0x2C] = Fixed(0),
        [0x2D] = Fixed(5),
        [0x2E] = Fixed(4),
        [0x2F] = Fixed(0),
        [0x30] = Fixed(0),
        [0x31] = Fixed(0),
        [0x32] = Fixed(0),

        [0x50] = Fixed(5),   // change ally options
        [0x51] = Fixed(9),   // transfer resources
        [0x60] = Var(r => { r.Skip(8); r.ReadCStringRaw(); return 0; }),  // map trigger chat command
        [0x61] = Fixed(0, apm: true),   // ESC pressed
        [0x62] = Fixed(12),  // scenario trigger
        [0x66] = Fixed(0, apm: true),   // enter hero skill submenu
        [0x67] = Fixed(0, apm: true),   // enter building submenu
        [0x68] = Fixed(12),  // minimap signal (ping)
        [0x69] = Fixed(16),  // continue game (block B)
        [0x6A] = Fixed(16),  // continue game (block A)
        [0x75] = Fixed(1),   // unknown
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

            if (!Actions.TryGetValue(action, out var def))
            {
                // 未知动作 id：无法确定长度，安全起见放弃本切片剩余部分。
                unknown++;
                break;
            }

            // 0x10/0x11/0x12/0x14：能力/训练/建造/施法——读出 object-id 做深度提取。
            if (def.ObjectIdLen > 0)
            {
                int total = def.ObjectIdLen;
                if (total > r.Remaining) { unknown++; break; }
                r.ReadUInt16();                  // ability flags
                string? code = ReadObjectId(r);  // 消费 4 字节
                r.Skip(total - 6);

                stats.TotalActions++;
                if (def.CountsApm) CountApm(stats, timeMs);
                if (code != null) Record(stats, code, timeMs);
                continue;
            }

            int len;
            try
            {
                len = def.Read != null ? def.Read(r) : def.FixedLen;
            }
            catch (ReplayParseException)
            {
                // 越界（变长动作读取失败）——本切片到此为止。
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
            if (def.CountsApm) CountApm(stats, timeMs);
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
}
