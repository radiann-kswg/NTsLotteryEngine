using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace NTsLotoEngine
{
    /// <summary>創作DB 1 レコードのうち、球の表示・結果 JSON に使う分だけ。</summary>
    [Serializable]
    public class NtCharacter
    {
        public string db;          // Primary / SemiPrimary / SelfSecondary
        public string num;         // "000" / "2-alt" / "3x11" など
        public string badge;       // Num_Badge
        public string nameJP;      // Name_JP 1 行目（"バイナ\n2(ツギ)" → "バイナ"）
        public string nameEN;      // Name_EN 1 行目
        public string shortEN;     // HUD 用（英数字のみのフォント）。"000(Thouser)" → "Thouser"、"Binor" → "Binor"
        public string progress;
        public string themeHex;    // ColorPalette の Primary（無ければ空）
        public string[] corefolder; // corefolder 画像の絶対パス（存在するものだけ）
    }

    /// <summary>
    /// 創作DB（サブモジュール 100BeautiesLab_CreationsDB）のナンバーテールズ 3DB を読む最小ローダ。
    /// NTsWallpaperEngine の CreationsDbLoader と同じ探索順（環境変数 → StreamingAssets/CreationsDB → サブモジュール直読み）と
    /// 同じ公開基準（Progress が released / released(beta) / stillTentative / unreleased 以外は返さない。AGENTS.md 4.5）。
    /// </summary>
    public static class CreationsDb
    {
        public const string EnvVar = "NTSLOTO_CREATIONSDB";
        public const string SubmoduleRoot = "100BeautiesLab_CreationsDB/data/Works_NumberTales";
        static readonly string[] Dbs = { "Primary", "SemiPrimary", "SelfSecondary" };
        public static readonly HashSet<string> ShownProgress = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "released", "released(beta)", "stillTentative", "unreleased" };

        static Dictionary<(string db, string num), NtCharacter> index;

        public static string ResolveRoot()
        {
            foreach (var root in new[]
            {
                Environment.GetEnvironmentVariable(EnvVar),
                Path.Combine(Application.streamingAssetsPath, "CreationsDB"),
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", SubmoduleRoot)),
            })
                if (!string.IsNullOrEmpty(root) && Directory.Exists(Path.Combine(root, "DataBases"))) return root;
            return null;
        }

        /// <summary>(db, num) のレコード。未公開・不在・DB 未取得なら null（呼び側は名前無しで続行する）。</summary>
        public static NtCharacter Find(string db, string num)
        {
            if (index == null) Load();
            return index.TryGetValue((db ?? "", num ?? ""), out var c) ? c : null;
        }

        static void Load()
        {
            index = new Dictionary<(string, string), NtCharacter>();
            string root = ResolveRoot();
            if (root == null) { Debug.LogWarning("[CreationsDb] not found (scripts/setup-submodule を実行)。球のキャラ名は無しで進む"); return; }
            foreach (var db in Dbs)
            {
                string path = Path.Combine(root, "DataBases", $"db_{db}.json");
                if (!File.Exists(path)) continue;
                JArray arr;
                try { arr = JArray.Parse(File.ReadAllText(path)); }
                catch (Exception e) { Debug.LogError($"[CreationsDb] {path}: {e.Message}"); continue; }
                foreach (var t in arr)
                {
                    if (t is not JObject o || !ShownProgress.Contains((string)o["Progress"] ?? "")) continue;
                    var c = Parse(o, db, root);
                    index[(db, c.num)] = c;
                }
            }
            Debug.Log($"[CreationsDb] {index.Count} records ({root})");
        }

        static NtCharacter Parse(JObject o, string db, string root)
        {
            string Str(string k) => o[k] == null || o[k].Type == JTokenType.Null ? "" : o[k].ToString();
            string First(string s) => s.Split('\n')[0].Trim();
            var c = new NtCharacter
            {
                db = db, num = Str("Num"), badge = Str("Num_Badge"), progress = Str("Progress"),
                nameJP = First(Str("Name_JP")), nameEN = First(Str("Name_EN")),
            };
            var m = Regex.Match(c.nameEN, @"^\S+\((.+)\)$");   // "000(Thouser)" → "Thouser"
            c.shortEN = m.Success ? m.Groups[1].Value : c.nameEN;
            if (o["ColorPalette"] is JArray pal)
                foreach (var p in pal)
                    if (p is JObject po && (string)po["Role"] == "#ColorRole_Primary" && !string.IsNullOrEmpty((string)po["Hex"]))
                    { c.themeHex = (string)po["Hex"]; break; }
            var paths = new List<string>();
            if (o["Images"]?["corefolder_PNGPath"] is JArray imgs)
                foreach (var rel in imgs)
                {
                    string abs = Path.Combine(root, "Images", $"DB_{db}", "corefolder", rel.ToString().Replace('/', Path.DirectorySeparatorChar) + ".png");
                    if (File.Exists(abs)) paths.Add(abs);
                }
            c.corefolder = paths.ToArray();
            return c;
        }
    }
}
