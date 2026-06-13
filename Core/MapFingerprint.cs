using System.Security.Cryptography;

namespace W3GAnalyzer.Core;

/// <summary>地图对比结果。</summary>
public sealed class MapCompareResult
{
    public string DraggedPath { get; set; } = "";
    public long DraggedSize { get; set; }
    public string DraggedSha { get; set; } = "";

    public string ReplayMapName { get; set; } = "";   // 录像里的地图名（不含扩展名）
    public string ReplayMapPath { get; set; } = "";   // 录像里的相对路径
    public uint ReplayChecksum { get; set; }

    public bool NameMatch { get; set; }

    public string? ReferencePath { get; set; }        // 按录像路径在本机定位到的地图文件
    public long ReferenceSize { get; set; }
    public string ReferenceSha { get; set; } = "";

    /// <summary>拖入地图与本机定位到的地图是否字节一致；无参照时为 null。</summary>
    public bool? FingerprintMatch { get; set; }

    public string Verdict { get; set; } = "";
}

/// <summary>
/// 地图文件指纹与对比。用整文件 SHA-256 作为指纹判断两个 .w3x 是否完全相同——
/// 不依赖录像里那个跨补丁不可复现的校验和，稳定可靠。
/// </summary>
public static class MapFingerprint
{
    public static string Sha256(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
    }

    /// <summary>
    /// 按录像里的相对地图路径（如 Maps\FrozenThrone\(4)TwistedMeadows.w3x）在本机定位实际文件：
    /// 从录像文件所在目录逐级向上，找哪个祖先目录下存在该相对路径。
    /// </summary>
    public static string? LocateReplayMapOnDisk(string replayFilePath, string mapRelativePath)
    {
        if (string.IsNullOrWhiteSpace(mapRelativePath)) return null;
        string rel = mapRelativePath.Replace('/', '\\').TrimStart('\\');

        var dir = Path.GetDirectoryName(Path.GetFullPath(replayFilePath));
        for (int i = 0; i < 10 && dir != null; i++)
        {
            string candidate = Path.Combine(dir, rel);
            if (File.Exists(candidate)) return candidate;

            // 也尝试只用文件名（地图可能被放在另一层）
            string byName = Path.Combine(dir, Path.GetFileName(rel));
            if (File.Exists(byName)) return byName;

            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    public static MapCompareResult Compare(ReplaySummary replay, string draggedMapPath)
    {
        var r = new MapCompareResult
        {
            DraggedPath = draggedMapPath,
            DraggedSize = new FileInfo(draggedMapPath).Length,
            DraggedSha = Sha256(draggedMapPath),
            ReplayMapName = replay.MapName,
            ReplayMapPath = replay.MapPath,
            ReplayChecksum = replay.MapChecksum,
        };

        string draggedName = Path.GetFileNameWithoutExtension(draggedMapPath);
        r.NameMatch = string.Equals(draggedName, replay.MapName, StringComparison.OrdinalIgnoreCase);

        string? refPath = LocateReplayMapOnDisk(replay.FilePath, replay.MapPath);
        if (refPath != null && !string.Equals(Path.GetFullPath(refPath),
                Path.GetFullPath(draggedMapPath), StringComparison.OrdinalIgnoreCase))
        {
            r.ReferencePath = refPath;
            r.ReferenceSize = new FileInfo(refPath).Length;
            r.ReferenceSha = Sha256(refPath);
            r.FingerprintMatch = string.Equals(r.ReferenceSha, r.DraggedSha, StringComparison.OrdinalIgnoreCase);
        }
        else if (refPath != null)
        {
            // 拖入的就是录像路径上的同一个文件
            r.ReferencePath = refPath;
            r.ReferenceSize = r.DraggedSize;
            r.ReferenceSha = r.DraggedSha;
            r.FingerprintMatch = true;
        }

        r.Verdict = BuildVerdict(r);
        return r;
    }

    private static string BuildVerdict(MapCompareResult r)
    {
        if (r.FingerprintMatch == true)
            return "✓ 一致：拖入地图与录像引用的地图文件完全相同（SHA-256 相同）";
        if (r.FingerprintMatch == false)
            return r.NameMatch
                ? "✗ 不一致：文件名相同但内容不同（SHA-256 不同），是同名的不同版本地图"
                : "✗ 不一致：拖入的地图与录像所用地图是不同的地图（文件名与内容均不同）";
        // 没有定位到本机参照地图
        return r.NameMatch
            ? "～ 仅文件名一致：本机未在录像路径定位到参照地图，无法按内容比对"
            : "✗ 文件名不一致：拖入的地图与录像所用地图名称不同";
    }
}
