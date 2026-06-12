namespace W3GAnalyzer.Core;

/// <summary>
/// 解码 w3g 静态数据里那段被混淆过的设置字符串。混淆规则：每 8 字节一组，
/// 组内第 0 个字节是掩码；其余位置 i（1..7）的真实字节为——若掩码的第 (i%8)
/// 位为 0 则是 (b-1)，否则是 b。掩码字节本身不输出。
/// </summary>
public static class EncodedStringDecoder
{
    public static byte[] Decode(byte[] encoded)
    {
        var output = new List<byte>(encoded.Length);
        byte mask = 0;
        for (int i = 0; i < encoded.Length; i++)
        {
            if (i % 8 == 0)
            {
                mask = encoded[i];
            }
            else
            {
                if ((mask & (1 << (i % 8))) == 0)
                    output.Add((byte)(encoded[i] - 1));
                else
                    output.Add(encoded[i]);
            }
        }
        return output.ToArray();
    }
}
