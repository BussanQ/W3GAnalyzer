namespace W3GAnalyzer.Core;

/// <summary>
/// .w3g 解压数据流里的块 ID（record id）。集中定义以取代解析器里散落的裸
/// <c>0x</c> 字面量。值对应 patch &gt;=1.07 的经典格式（非重制版）。
/// </summary>
internal enum BlockId : byte
{
    /// <summary>流末尾填充，遇到即停止。</summary>
    EndPadding = 0x00,

    /// <summary>静态数据区里的玩家记录（额外玩家循环用）。</summary>
    PlayerRecord = 0x16,

    /// <summary>玩家离场（LeaveGame）。</summary>
    LeaveGame = 0x17,

    /// <summary>对局开始记录（GameStartRecord，含槽位表）。</summary>
    GameStartRecord = 0x19,

    /// <summary>未知块，负载 4 字节（与 0x1B/0x1C 同处理）。</summary>
    Unknown1A = 0x1A,
    Unknown1B = 0x1B,
    Unknown1C = 0x1C,

    /// <summary>时间片（TimeSlot），承载玩家命令数据。两种 id 同义。</summary>
    TimeSlotA = 0x1E,
    TimeSlotB = 0x1F,

    /// <summary>聊天消息（ChatMessage）。</summary>
    ChatMessage = 0x20,

    /// <summary>未知块，负载长度由首字节给出。</summary>
    Unknown22 = 0x22,

    /// <summary>未知块，固定 10 字节负载。</summary>
    Unknown23 = 0x23,

    /// <summary>对局倒计时/结束相关，固定 8 字节负载。</summary>
    Countdown2F = 0x2F,
}
