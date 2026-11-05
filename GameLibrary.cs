using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;

namespace RadeonTune
{
    public class Game
    {
        public string Name { get; set; } = "";
        public string ExePath { get; set; } = "";
        public string WorkDir { get; set; } = "";
        public string Source { get; set; } = "manual";
        public string AppId { get; set; }
        public string CustomCover { get; set; } = "";
    }

    public static class GameLibrary
    {
        private static string FilePath => Path.Combine(Engine.StoreDir, "games.json");

        public static List<Game> Load() => Engine.Load(FilePath, new List<Game>()) ?? new List<Game>();

        public static void Save(List<Game> games) => Engine.Save(FilePath, games);

        /// <summary>扫描 Steam 与 Epic，保留已有的手动条目。</summary>
        public static List<Game> Scan()
        {
            var found = new List<Game>();
            found.AddRange(ScanSteam());
            found.AddRange(ScanEpic());

            var existing = Load();
            var manual = existing.Where(g => g.Source == "manual");
            // 去重：同一 exe 只留一条
            var all = new List<Game>();
            foreach (var g in found.Concat(manual))
            {
                if (string.IsNullOrWhiteSpace(g.ExePath)) continue;
                g.ExePath = Normalize(g.ExePath);
                if (all.Any(x => string.Equals(Normalize(x.ExePath), g.ExePath, StringComparison.OrdinalIgnoreCase))) continue;
                all.Add(g);
            }
            Save(all);
            return all;
        }

        public static string Normalize(string path)
        {
            try { return Path.GetFullPath(path).TrimEnd('\\'); }
            catch { return path; }
        }

        private static IEnumerable<Game> ScanSteam()
        {
            var result = new List<Game>();
            try
            {
                string steamPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
                if (string.IsNullOrEmpty(steamPath)) return result;

                var libraries = new List<string> { steamPath };
                var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdf))
                {
                    foreach (var line in File.ReadAllLines(vdf))
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(line, "\"path\"\\s+\"([^\"]+)\"");
                        if (m.Success)
                        {
                            var p = m.Groups[1].Value.Replace("\\\\", "\\");
                            if (!libraries.Contains(p, StringComparer.OrdinalIgnoreCase)) libraries.Add(p);
                        }
                    }
                }

                foreach (var lib in libraries)
                {
                    var apps = Path.Combine(lib, "steamapps");
                    if (!Directory.Exists(apps)) continue;
                    foreach (var acf in Directory.GetFiles(apps, "*.acf"))
                    {
                        string name = null, installDir = null;
                        foreach (var line in File.ReadAllLines(acf))
                        {
                            var n = System.Text.RegularExpressions.Regex.Match(line, "\"name\"\\s+\"([^\"]+)\"");
                            var d = System.Text.RegularExpressions.Regex.Match(line, "\"installdir\"\\s+\"([^\"]+)\"");
                            if (n.Success) name = n.Groups[1].Value;
                            if (d.Success) installDir = d.Groups[1].Value;
                        }
                        if (string.IsNullOrEmpty(installDir)) continue;
                        var dir = Path.Combine(apps, "common", installDir);
                        if (!Directory.Exists(dir)) continue;
                        var exe = FindBestExe(dir);
                        if (exe == null) continue;
                        var appId = System.IO.Path.GetFileNameWithoutExtension(acf);
                        if (appId.StartsWith("appmanifest_")) appId = appId.Substring("appmanifest_".Length);
                        result.Add(new Game { Name = name ?? installDir, ExePath = exe, WorkDir = Path.GetDirectoryName(exe), Source = "steam", AppId = appId });
                    }
                }
            }
            catch { }
            return result;
        }

        private static IEnumerable<Game> ScanEpic()
        {
            var result = new List<Game>();
            try
            {
                var manifests = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Epic", "EpicGamesLauncher", "Data", "Manifests");
                if (!Directory.Exists(manifests)) return result;

                foreach (var item in Directory.GetFiles(manifests, "*.item"))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(File.ReadAllText(item));
                        var root = doc.RootElement;
                        string display = root.TryGetProperty("DisplayName", out var dn) ? dn.GetString() : null;
                        string loc = root.TryGetProperty("InstallLocation", out var il) ? il.GetString() : null;
                        string launch = root.TryGetProperty("LaunchExecutable", out var le) ? le.GetString() : null;
                        if (string.IsNullOrEmpty(loc) || !Directory.Exists(loc)) continue;
                        string exe = launch != null ? Path.Combine(loc, launch) : FindBestExe(loc);
                        if (exe == null || !File.Exists(exe)) continue;
                        result.Add(new Game { Name = display ?? Path.GetFileName(loc), ExePath = exe, WorkDir = Path.GetDirectoryName(exe), Source = "epic" });
                    }
                    catch { }
                }
            }
            catch { }
            return result;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        /// <summary>手动添加的游戏没有 AppId，用可执行文件图标当封面。</summary>
        public static string EnsureManualCover(Game g)
        {
            try
            {
                if (string.IsNullOrEmpty(g.ExePath) || !File.Exists(g.ExePath)) return null;
                var dir = Path.Combine(Engine.StoreDir, "covers");
                Directory.CreateDirectory(dir);
                using var sha = System.Security.Cryptography.SHA1.Create();
                var hash = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(g.ExePath.ToLowerInvariant())))
                                       .Replace("-", "").Substring(0, 16);
                var outPath = Path.Combine(dir, "manual_" + hash + ".png");
                if (File.Exists(outPath)) return outPath;

                var info = new SHFILEINFO();
                SHGetFileInfo(g.ExePath, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), 0x000000100 | 0x000000000);
                if (info.hIcon == IntPtr.Zero) return null;
                var src = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                DestroyIcon(info.hIcon);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(src));
                using (var fs = File.Create(outPath)) enc.Save(fs);
                return outPath;
            }
            catch { return null; }
        }


        /// <summary>把任意图片缩放后另存为封面副本，返回副本路径。</summary>
        public static string SaveCoverCopy(string srcPath, string exePath)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 600;
                bmp.UriSource = new Uri(srcPath, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();

                var dir = Path.Combine(Engine.StoreDir, "covers");
                Directory.CreateDirectory(dir);
                using var sha = System.Security.Cryptography.SHA1.Create();
                var hash = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes((exePath ?? srcPath).ToLowerInvariant())))
                                       .Replace("-", "").Substring(0, 16);
                var outPath = Path.Combine(dir, "custom_" + hash + ".png");
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bmp));
                using (var fs = File.Create(outPath)) enc.Save(fs);
                return outPath;
            }
            catch { return srcPath; }
        }

        public static string CoverFile(Game g, string kind = "thumb")
        {
            if (kind == "thumb" && !string.IsNullOrEmpty(g.CustomCover) && File.Exists(g.CustomCover))
                return g.CustomCover;
            if (string.IsNullOrEmpty(g.AppId))
                return kind == "thumb" ? EnsureManualCover(g) : null;
            return Path.Combine(Engine.StoreDir, "covers", g.AppId + (kind == "thumb" ? "" : "_" + kind) + ".jpg");
        }

        public static async System.Threading.Tasks.Task<string> EnsureCoverAsync(Game g, string kind = "thumb")
        {
            if (string.IsNullOrEmpty(g.AppId)) return null;
            var path = CoverFile(g, kind);
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length > 2000) return path;
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                using var http = new System.Net.Http.HttpClient(
                    new System.Net.Http.HttpClientHandler { UseProxy = false })
                { Timeout = TimeSpan.FromSeconds(20) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

                // 新版 Steam 资源路径需要先查 store API 拿到带 hash 的 URL
                var urls = new List<string>();
                try
                {
                    var json = await http.GetStringAsync(
                        $"https://store.steampowered.com/api/appdetails?appids={g.AppId}&filters=basic").ConfigureAwait(false);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var root = doc.RootElement.GetProperty(g.AppId);
                    if (root.GetProperty("success").GetBoolean())
                    {
                        var data = root.GetProperty("data");
                        foreach (var key in new[] { "header_image", "capsule_imagev5", "capsule_image" })
                        {
                            if (data.TryGetProperty(key, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                var u = v.GetString();
                                if (!string.IsNullOrEmpty(u)) urls.Add(u);
                            }
                        }
                    }
                }
                catch { }

                // 旧版 CDN 兜底
                urls.Add($"https://cdn.cloudflare.steamstatic.com/steam/apps/{g.AppId}/header.jpg");
                urls.Add($"https://cdn.cloudflare.steamstatic.com/steam/apps/{g.AppId}/library_600x900.jpg");

                foreach (var u in urls)
                {
                    try
                    {
                        var bytes = await http.GetByteArrayAsync(u).ConfigureAwait(false);
                        if (bytes.Length < 2000) continue;
                        await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);
                        return path;
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        /// <summary>目录内最大的 exe（通常是游戏本体）。</summary>
        public static string FindBestExe(string dir, int depth = 3)
        {
            try
            {
                var bad = new[] { "uninstall", "setup", "install", "launcher", "config", "crash", "report",
                                  "update", "repair", "dxsetup", "vcredist", "unitycrash", "error", "helper",
                                  "elevate", "activation", "webview", "cef", "bootstrap" };
                var files = Directory.GetFiles(dir, "*.exe", new EnumerationOptions
                {
                    RecurseSubdirectories = depth > 0,
                    MaxRecursionDepth = depth,
                    IgnoreInaccessible = true,
                });

                string folderName = new DirectoryInfo(dir).Name.ToLowerInvariant();
                var scored = new List<(string Path, long Score)>();
                foreach (var f in files)
                {
                    var name = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                    if (bad.Any(b => name.Contains(b))) continue;

                    long score = new FileInfo(f).Length;
                    if (name == folderName) score += 1_000_000_000;
                    if (name.Contains("shipping") || name.Contains("win64")) score += 300_000_000;
                    if (string.Equals(Path.GetDirectoryName(f), dir, StringComparison.OrdinalIgnoreCase)) score += 150_000_000;
                    scored.Add((f, score));
                }
                if (scored.Count == 0) return null;
                return scored.OrderByDescending(x => x.Score).First().Path;
            }
            catch { return null; }
        }

        /// <summary>手动添加：给定文件夹，找出最可能的 exe。</summary>
        public static Game AddFolder(string dir)
        {
            if (!Directory.Exists(dir)) return null;
            var exe = FindBestExe(dir, 1);
            if (exe == null) return null;
            var g = new Game
            {
                Name = new DirectoryInfo(dir).Name,
                ExePath = exe,
                WorkDir = dir,
                Source = "manual",
            };
            var all = Load();
            if (!all.Any(x => string.Equals(x.ExePath, exe, StringComparison.OrdinalIgnoreCase))) all.Add(g);
            Save(all);
            return g;
        }
    }
}
