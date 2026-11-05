using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RadeonTune
{
    public class FsrHit
    {
        public string File = "";
        public string Kind = "";          // 超分 / 帧生成
        public string Version = "";       // 例如 3.1.1
    }

    /// <summary>
    /// 通过游戏目录里的 AMD FidelityFX 运行时 DLL 判断 FSR 支持情况。
    /// FSR 3.x 会带 amd_fidelityfx_upscaler_dx12.dll 与 amd_fidelityfx_framegeneration_dx12.dll。
    /// </summary>
    public static class FsrDetector
    {
        private static readonly (string Pattern, string Kind)[] Files =
        {
            ("amd_fidelityfx_upscaler_dx12.dll", "超分"),
            ("amd_fidelityfx_framegeneration_dx12.dll", "帧生成"),
            ("amd_fidelityfx_fsr3*.dll", "超分"),
            ("ffx_fsr3*.dll", "超分"),
        };

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, List<FsrHit>> Cache = new();

        public static List<FsrHit> ScanCached(string dir) =>
            Cache.GetOrAdd(dir ?? "", d => Scan(d));

        public static List<FsrHit> Scan(string dir, int depth = 4)
        {
            var hits = new List<FsrHit>();
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return hits;
            try
            {
                foreach (var (pattern, kind) in Files)
                {
                    IEnumerable<string> files;
                    try
                    {
                        files = Directory.EnumerateFiles(dir, pattern, new EnumerationOptions
                        {
                            RecurseSubdirectories = depth > 0,
                            MaxRecursionDepth = depth,
                            IgnoreInaccessible = true,
                        });
                    }
                    catch { continue; }

                    foreach (var f in files.Take(6))
                    {
                        string ver = "";
                        try
                        {
                            var fv = FileVersionInfo.GetVersionInfo(f);
                            ver = !string.IsNullOrWhiteSpace(fv.ProductVersion) ? fv.ProductVersion
                                : !string.IsNullOrWhiteSpace(fv.FileVersion) ? fv.FileVersion : "";
                        }
                        catch { }
                        var norm = NormalizeFsrVersion(ver);
                        if (hits.Any(h => h.File == Path.GetFileName(f) && h.Kind == kind)) continue;
                        hits.Add(new FsrHit { File = Path.GetFileName(f), Kind = kind, Version = norm });
                    }
                }
            }
            catch { }
            return hits;
        }

        /// <summary>把 DLL 版本号归一到 FSR 大版本（3.0 / 3.1 等）。</summary>
        public static string NormalizeFsrVersion(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "版本未知";
            var parts = raw.Split('.', ',', ' ');
            if (parts.Length >= 2 && int.TryParse(parts[0], out var major) && int.TryParse(parts[1], out var minor))
            {
                if (major == 1 && minor <= 1) return "3.0";          // FSR3 运行时早期版本号 1.0/1.1
                if (major == 3) return $"3.{minor}";
                return $"{major}.{minor}";
            }
            return raw.Trim();
        }
    }
}
