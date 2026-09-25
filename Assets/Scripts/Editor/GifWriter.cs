using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace NTsLotteryEngine.EditorTools
{
    /// <summary>
    /// 依存なしの最小 GIF89a エンコーダ（README のボール回転 GIF 用。Windows に ffmpeg が無くてもエディタだけで完結させる）。
    /// 全フレーム共通の 256 色パレット（15bit ヒストグラムのメディアンカット）・無限ループ・LZW。ディザは掛けない。
    /// </summary>
    public static class GifWriter
    {
        /// <param name="frames">各フレームの画素（上の行から・w*h 個）</param>
        /// <param name="delayCs">1 フレームの表示時間 [1/100 秒]</param>
        public static void Write(string path, IList<Color32[]> frames, int w, int h, int delayCs)
        {
            var palette = MedianCut(frames, 256, out var lut);
            using var bw = new BinaryWriter(File.Create(path));
            bw.Write(Encoding.ASCII.GetBytes("GIF89a"));
            bw.Write((ushort)w); bw.Write((ushort)h);
            bw.Write((byte)0xF7);                          // グローバルパレットあり・256 色
            bw.Write((byte)0); bw.Write((byte)0);          // 背景色 / アスペクト
            for (int i = 0; i < 256; i++)
            {
                var c = i < palette.Length ? palette[i] : default;
                bw.Write(c.r); bw.Write(c.g); bw.Write(c.b);
            }
            bw.Write(new byte[] { 0x21, 0xFF, 0x0B }); bw.Write(Encoding.ASCII.GetBytes("NETSCAPE2.0"));
            bw.Write(new byte[] { 0x03, 0x01, 0x00, 0x00, 0x00 });   // ループ回数 0 = 無限

            var idx = new byte[w * h];
            foreach (var f in frames)
            {
                bw.Write(new byte[] { 0x21, 0xF9, 0x04, 0x04 });       // GCE: disposal=1（残す）・透過なし
                bw.Write((ushort)delayCs); bw.Write((byte)0); bw.Write((byte)0);
                bw.Write((byte)0x2C); bw.Write((ushort)0); bw.Write((ushort)0);
                bw.Write((ushort)w); bw.Write((ushort)h); bw.Write((byte)0);
                for (int i = 0; i < idx.Length; i++) idx[i] = lut[Key(f[i])];
                Lzw(bw, idx);
            }
            bw.Write((byte)0x3B);
        }

        static int Key(Color32 c) => (c.r >> 3) << 10 | (c.g >> 3) << 5 | c.b >> 3;
        static int Ch(int key, int ch) => (key >> (10 - ch * 5)) & 31;

        /// <summary>15bit に丸めた色のヒストグラムをメディアンカットで n 色へ。lut は 15bit キー → パレット番号。</summary>
        static Color32[] MedianCut(IList<Color32[]> frames, int n, out byte[] lut)
        {
            var count = new int[32768];
            var sum = new long[32768 * 3];
            foreach (var f in frames)
                foreach (var c in f)
                {
                    int k = Key(c); count[k]++;
                    sum[k * 3] += c.r; sum[k * 3 + 1] += c.g; sum[k * 3 + 2] += c.b;
                }
            var cols = new List<int>();
            for (int k = 0; k < 32768; k++) if (count[k] > 0) cols.Add(k);
            var arr = cols.ToArray();

            var boxes = new List<(int s, int e)> { (0, arr.Length) };
            while (boxes.Count < n)
            {
                int best = -1, bestCh = 0; long bestScore = 0;
                for (int b = 0; b < boxes.Count; b++)
                {
                    var (s, e) = boxes[b];
                    if (e - s < 2) continue;
                    long px = 0; int ch = 0, range = -1;
                    for (int c = 0; c < 3; c++)
                    {
                        int lo = 31, hi = 0;
                        for (int i = s; i < e; i++) { int v = Ch(arr[i], c); if (v < lo) lo = v; if (v > hi) hi = v; }
                        if (hi - lo > range) { range = hi - lo; ch = c; }
                    }
                    for (int i = s; i < e; i++) px += count[arr[i]];
                    long score = px * (range + 1);
                    if (range > 0 && score > bestScore) { bestScore = score; best = b; bestCh = ch; }
                }
                if (best < 0) break;
                var (bs, be) = boxes[best];
                int sc = bestCh;
                Array.Sort(arr, bs, be - bs, Comparer<int>.Create((x, y) => Ch(x, sc).CompareTo(Ch(y, sc))));
                long total = 0; for (int i = bs; i < be; i++) total += count[arr[i]];
                long acc = 0; int mid = bs + 1;
                for (int i = bs; i < be - 1; i++) { acc += count[arr[i]]; mid = i + 1; if (acc * 2 >= total) break; }
                boxes[best] = (bs, mid); boxes.Add((mid, be));
            }

            var pal = new Color32[boxes.Count];
            for (int b = 0; b < boxes.Count; b++)
            {
                long r = 0, g = 0, bl = 0, px = 0;
                for (int i = boxes[b].s; i < boxes[b].e; i++)
                { int k = arr[i]; r += sum[k * 3]; g += sum[k * 3 + 1]; bl += sum[k * 3 + 2]; px += count[k]; }
                pal[b] = px > 0 ? new Color32((byte)(r / px), (byte)(g / px), (byte)(bl / px), 255) : default;
            }

            lut = new byte[32768];
            foreach (int k in arr)
            {
                int r = Ch(k, 0) * 8 + 4, g = Ch(k, 1) * 8 + 4, b = Ch(k, 2) * 8 + 4, bi = 0, bd = int.MaxValue;
                for (int p = 0; p < pal.Length; p++)
                {
                    int dr = pal[p].r - r, dg = pal[p].g - g, db = pal[p].b - b, d = dr * dr + dg * dg + db * db;
                    if (d < bd) { bd = d; bi = p; }
                }
                lut[k] = (byte)bi;
            }
            return pal;
        }

        /// <summary>画像データ（最小コード長 8・可変長 9〜12bit・255 バイトのサブブロック）。</summary>
        static void Lzw(BinaryWriter bw, byte[] idx)
        {
            const int Clear = 256, Eoi = 257;
            bw.Write((byte)8);
            var block = new byte[255]; int blen = 0;
            void Put(byte v) { block[blen++] = v; if (blen == 255) { bw.Write((byte)255); bw.Write(block); blen = 0; } }

            var dict = new Dictionary<int, int>(4096);
            int size = 9, next = 258, bits = 0, nbits = 0;
            void Emit(int code) { bits |= code << nbits; nbits += size; while (nbits >= 8) { Put((byte)bits); bits >>= 8; nbits -= 8; } }

            Emit(Clear);
            int prefix = idx[0];
            for (int i = 1; i < idx.Length; i++)
            {
                int k = idx[i], key = prefix << 8 | k;
                if (dict.TryGetValue(key, out var code)) { prefix = code; continue; }
                Emit(prefix);
                if (next < 4096) { dict[key] = next++; if (next > (1 << size) && size < 12) size++; }
                else { Emit(Clear); dict.Clear(); size = 9; next = 258; }
                prefix = k;
            }
            Emit(prefix); Emit(Eoi);
            if (nbits > 0) Put((byte)bits);
            if (blen > 0) { bw.Write((byte)blen); bw.Write(block, 0, blen); }
            bw.Write((byte)0);
        }
    }
}
