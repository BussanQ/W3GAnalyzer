namespace W3GAnalyzer.Core;

/// <summary>
/// 在一段 byte[] 上做带边界检查的小端读取。每个读取方法越界都会抛
/// <see cref="ReplayParseException"/> 并附带出错偏移，便于定位格式问题。
/// </summary>
public sealed class BinaryReaderEx
{
    private readonly byte[] _data;
    public int Position { get; private set; }

    public BinaryReaderEx(byte[] data, int start = 0)
    {
        _data = data;
        Position = start;
    }

    public int Length => _data.Length;
    public int Remaining => _data.Length - Position;
    public bool Eof => Position >= _data.Length;

    private void Need(int n)
    {
        if (Position + n > _data.Length)
            throw new ReplayParseException(
                $"读取越界：需要 {n} 字节，偏移 0x{Position:X} 处只剩 {Remaining} 字节");
    }

    /// <summary>不前移读取下一个字节；已到末尾返回 -1。</summary>
    public int Peek() => Position < _data.Length ? _data[Position] : -1;

    public byte ReadByte()
    {
        Need(1);
        return _data[Position++];
    }

    public ushort ReadUInt16()
    {
        Need(2);
        ushort v = (ushort)(_data[Position] | (_data[Position + 1] << 8));
        Position += 2;
        return v;
    }

    public uint ReadUInt32()
    {
        Need(4);
        uint v = (uint)(_data[Position]
                        | (_data[Position + 1] << 8)
                        | (_data[Position + 2] << 16)
                        | (_data[Position + 3] << 24));
        Position += 4;
        return v;
    }

    public float ReadSingle()
    {
        Need(4);
        float v = BitConverter.ToSingle(_data, Position);
        Position += 4;
        return v;
    }

    public byte[] ReadBytes(int n)
    {
        if (n < 0) throw new ReplayParseException($"非法长度 {n} @ 0x{Position:X}");
        Need(n);
        var buf = new byte[n];
        Array.Copy(_data, Position, buf, 0, n);
        Position += n;
        return buf;
    }

    public void Skip(int n)
    {
        if (n < 0) throw new ReplayParseException($"非法跳过长度 {n} @ 0x{Position:X}");
        Need(n);
        Position += n;
    }

    public void Seek(int pos)
    {
        if (pos < 0 || pos > _data.Length)
            throw new ReplayParseException($"定位越界 0x{pos:X}");
        Position = pos;
    }

    /// <summary>读到 0x00 终止符（含）为止，返回不含终止符的原始字节。</summary>
    public byte[] ReadCStringRaw()
    {
        int start = Position;
        while (Position < _data.Length && _data[Position] != 0x00)
            Position++;
        if (Position >= _data.Length)
            throw new ReplayParseException($"字符串未终止，始于 0x{start:X}");
        var buf = new byte[Position - start];
        Array.Copy(_data, start, buf, 0, buf.Length);
        Position++; // 跳过终止符
        return buf;
    }

    /// <summary>读 C 字符串并按 GBK/UTF-8 自动解码。</summary>
    public string ReadCString() => TextDecoder.Decode(ReadCStringRaw());
}
