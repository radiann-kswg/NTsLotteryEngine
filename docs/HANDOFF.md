# HANDOFF.md — 次セッションへの引継ぎ（2026-09-03 深夜 時点・制作その３）

> 読む順: `AGENTS.md`（運用・罠）→ `docs/DESIGN.md`（仕様・機構）→ 本ファイル（いまの状態と次にやること）。

## 1. いまの状態

- **7 塔 6 variant のコレクタを MC で校正済み**（有効数字 2 桁）。扇形の幅は `BlenderSources/kuruun_params.json` の `variants.<name>.win`、実測値は `docs/DESIGN.md` 2.1 節と `Output/mc_<variant>.json`。MC 治具の 2 つの穴（1 試行目のボウル中心落下・境界角の 3.75° 量子化）とコレクタの内縁 0.02 を直した（AGENTS.md 罠16〜18）。
- **脱線ゼロ**: 最終形で全経路 Play ×3（`Recordings/20260903-203225 / 203719 / 204820.mp4`）と塔だけの Play ×2 で `DerailWatch` 警告 0・stuck 再投入 0。直した原因は段間隔（1.25 → 1.45）・投入速度の座標系・シュートの向き・穴リングの桟（AGENTS.md 罠19〜21・23）。`DerailWatch` は自由落下（下向き 18m/s 未満）を除外し、塔の球がトレイ以外で地面に居る「escaped」を足した。最上段の投入は下段と同じ静止落下（罠24）。
- **篩のカメラ**: 抽選中の篩の正面 1.9m・FOV 40 で、高さは最下の球に追従（`SieveMachine.Follow`・`camAhead 0.25`・`camLerp 3`）。ガラスは alpha 0.10・smoothness 0.15（近接だと白飛びした）。
- **篩の公平性**: 2〜11 は番号順に生成すると最後に生成した球（11）が 6 回連続で最初に着いた（皿の桟が平らで静止した球が皿と一緒に回り続け、動いている球だけが落ちる）。皿の桟を屋根形（`sieves.*.ring_roof` 0.02）にし、投入位置と生成順を毎回シャッフル（AGENTS.md 罠23・25）。直後の Play は 7（slot 8）が勝ち。外周の slot が有利な傾向は残るかもしれないので、次の数回の結果を見ること。
- ブランチ `develop`。今日の変更は未コミット（User が Windows 側で）。
- 別ボール 81 は 9×9(クック)（Progress notProceeded のため名前は出ない）。テクスチャは User が Inspector で貼る。

## 2. 次にやること（優先順）

1. **全経路 Play を数回**回して脱線ゼロと 2〜11 の勝者のばらつきを確かめ続ける（`Tools > NTsLoto > Play + Record` → Console の Warning と `DerailWatch.events`、`[SieveU] spawn/arrived` ログ）。低頻度の詰まりが出たら録画の同時刻を切り出す（`ffmpeg -ss T -t 1 -vf fps=15`）。
2. 配管の Blender 化（漏斗・シュート・筒・チャンネル・レール）と見た目（材質・照明）。塔は 11 段 ≈ 16m なので全景カメラは要再検討。
3. `StreamingAssets/CreationsDB` 同期ツール、Linux ビルドの実機確認（未着手）。
4. MC と実機の突き合わせ（実機の到達統計を 1 度取る）。MC は投入条件（落差 0.39/0.59・接線 1.4）で p が変わらないことは確認済み。

## 3. 触るときの注意（要点。詳細は AGENTS.md 5 章）

- 形を変えるのは `kuruun_params.json` → Blender で `gen_kuruun.py` 実行（Blender MCP: `REPO=...; exec(open(...).read())`）→ Unity `AssetDatabase.Refresh` → `Build Loto Scene`。FBX の角度は Unity = Blender + 180°（鏡映なし）。
- **コレクタ・段間隔・減衰・材質を変えたら MC を回し直す**（`LotoMonteCarlo.RunBatch(new[]{...})` を RunCommand から。10 万試行 ≈ 75 秒。エディタが非アクティブだと update が間引かれるので `stepsPerUpdate` を 2500 に）。
- **当たりを作るコードは書かない**（喉へ置く・引き寄せる）。詰まり救済は投入点へ戻すだけ。
- Play 中に Assets のスクリプトを保存しない（再コンパイルで録画が落ちる）。MC 中もスクリプトを保存しない（ドメインリロードで MC が死に `Physics.simulationMode` が Script のまま残る。残ったら `Physics.simulationMode = SimulationMode.FixedUpdate` に戻す）。
- Cowork サンドボックスの git は lock を消せない。コミットは User が Windows 側で。

## 4. 主要ノブ

| どこ | 何 | 既定 |
| --- | --- | --- |
| `kuruun_params.json` | bowl（rim 1.0 / cone 0.28 / ring 0.45〜0.65 / 6 穴×36° / ring_roof 0.012）・collector（top −0.20 / drop 0.16 / rim_h 0.12 / 樋 0.74〜0.94・fall 0.35・tilt 0.02 / 出口 Blender 0°）・variants.win（校正済み・落差 0.59: p10 37.0 / p6of88 25.1 / p18 65.2 / p32 2×57.3 / p50 2×91.0 / p53 2×96.3）・sieves（L: 3 穴×28°、U: 2 穴×40°、ring_roof 0.02） | — |
| `KuruunTower` | `bowlRpm` / `dropVelocity`（ローカル。既定ゼロ = 下段と同じ静止落下） / `angularDamping` / `funnelDamping` / `timeout` / `camFov` | 10 / 0 / 0.05 / 2.0 / 25 / 40 |
| `SieveMachine` | `dishRpm` / `spawnRadius` / `camAhead` / `camFov` / `camLerp` | 8 / rim−0.15 / 0.25 / 40 / 3 |
| `LotoMonteCarlo` | `variant` / `trials` / `parallel` / `bowlRpm` / `entryR` / `entryHeight` / `entryTangential` / `seed` / `stepsPerUpdate` | p10 / 2000 / 64 / 10 / 0.85 / 0.59 / 0 / 1 / 2500 |
| `LotoSceneBuilder`（定数） | `TowerArcR` / `TowerPitch` / `TowerStagger` / `LowerLayers` / `UpperLayers` / `GutterR` / `GutterExitDepth` / `ChW` | 6.2 / 1.45 / 0.425 / 15 / 4 / 0.94 / 0.81 / 1.24 |
| `DerailWatch` | `maxSpeed`（水平・上向き） / `maxFallSpeed` / `minY` / `maxR` | 8 / 18 / −0.5 / 12 |
| `BallUtil` | `Bouncy`（球: 反発 0.6・摩擦 0.4）/ `Machine`（機械: 摩擦 0.3・反発 0.1） | — |
