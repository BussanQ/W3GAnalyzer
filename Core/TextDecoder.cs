using System.Text;

namespace W3GAnalyzer.Core;

/// <summary>
/// 把录像里的原始字节解码为字符串。国内平台录像里的玩家名 / 聊天 / 地图路径
/// 经常是 GBK(936) 编码而非 UTF-8，所以策略是：纯 ASCII 走快路；否则先用
/// 严格 UTF-8（遇非法字节抛异常），失败再回退 GBK。顺序不能反——GBK 几乎
/// 不会拒绝任何字节序列，先试 GBK 会把本是 UTF-8 的内容解成乱码。
/// </summary>
public static class TextDecoder
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, throwOnInvalidBytes: true);
    private static Encoding? _gbk;

    /// <summary>必须在程序入口调用一次，注册 CodePages 提供程序后 GBK(936) 才可用。</summary>
    public static void EnsureProviderRegistered()
    {
        if (_gbk != null) return;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _gbk = Encoding.GetEncoding(936);
    }

    public static string Decode(byte[] raw)
    {
        if (raw.Length == 0) return string.Empty;

        bool ascii = true;
        foreach (var b in raw)
        {
            if (b >= 0x80) { ascii = false; break; }
        }
        if (ascii) return Encoding.ASCII.GetString(raw);

        try
        {
            return StrictUtf8.GetString(raw);
        }
        catch (DecoderFallbackException)
        {
            EnsureProviderRegistered();
            return _gbk!.GetString(raw);
        }
    }
}
