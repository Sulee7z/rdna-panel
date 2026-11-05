using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace RadeonTune
{
    /// <summary>从 SteamGridDB 拉取封面（需要用户自己的 API Key，免费）。</summary>
    public static class CoverFetcher
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
        private static string KeyPath => Path.Combine(Engine.StoreDir, "sgdb.key");
        private static string CacheDir => Path.Combine(Engine.StoreDir, "covers");

        public static string LoadKey()
        {
            try { return File.Exists(KeyPath) ? File.ReadAllText(KeyPath).Trim() : ""; }
            catch { return ""; }
        }

        public static void SaveKey(string key)
        {
            try
            {
                Directory.CreateDirectory(Engine.StoreDir);
                File.WriteAllText(KeyPath, (key ?? "").Trim());
            }
            catch { }
        }

        /// <summary>按游戏名搜索并下载封面，返回本地文件路径。</summary>
        public static async Task<string> FetchAsync(string name)
        {
            var key = LoadKey();
            if (string.IsNullOrEmpty(key)) return null;

            Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("RDNAPanel/1.0");

            var search = await Http.GetStringAsync(
                "https://www.steamgriddb.com/api/v2/search/autocomplete/" + Uri.EscapeDataString(name));
            using var sd = JsonDocument.Parse(search);
            if (!sd.RootElement.GetProperty("success").GetBoolean()) return null;
            var arr = sd.RootElement.GetProperty("data");
            if (arr.GetArrayLength() == 0) return null;
            int gameId = arr[0].GetProperty("id").GetInt32();

            var grids = await Http.GetStringAsync(
                $"https://www.steamgriddb.com/api/v2/grids/game/{gameId}?dimensions=600x900,342x482,460x215");
            using var gd = JsonDocument.Parse(grids);
            if (!gd.RootElement.GetProperty("success").GetBoolean()) return null;
            var garr = gd.RootElement.GetProperty("data");
            if (garr.GetArrayLength() == 0) return null;
            var url = garr[0].GetProperty("url").GetString();
            if (string.IsNullOrEmpty(url)) return null;

            var bytes = await Http.GetByteArrayAsync(url);
            Directory.CreateDirectory(CacheDir);
            var outPath = Path.Combine(CacheDir, "sgdb_" + gameId + ".png");
            await File.WriteAllBytesAsync(outPath, bytes);
            return outPath;
        }
    }
}
