# HANDOFF.md — 次セッションへの引継ぎ（2026-09-03 夜 時点）

> 読む順: `AGENTS.md`（運用・罠）→ `docs/DESIGN.md`（仕様・機構）→ 本ファイル（いまの状態と次にやること）。

## 1. いまの状態

- **仕様を物理抽選に切り替えた**（User 指示）。RNG 先行は廃止。2〜11（4 層）・12〜99（15 層）の篩型ロトマシーンと、回転ボウル＋静止コレクタのクルーン 7 塔。**全経路が Play で通り、`result.json` が出る**（約 3.5 分。篩は 30 秒で 7 球、塔は 1 段 3〜10 秒）。
- 抽選機メッシュは Blender 生成（`BlenderSources/gen_kuruun.py` + `kuruun_params.json` → `Assets/Models/*.fbx`）。配管（漏斗・シュート・筒・レール）はまだ ProcMesh/Box。
- MC 治具 `Tools > NTsLoto > Monte Carlo` が動く（p50 で n=1024・p=0.518±0.03・timeout 0・1 分）。**7 variant の本計測（n≈4 万）は未実施** → DESIGN.md 2.1 節に記録する。
- 脱線監視 `DerailWatch` 付き。直近の全経路 Play での警告: 塔で「stuck → 再投入」が 2 件（10 段・11 段の塔の 2 段目。再投入で通った）、落下チャンネル内の自由落下 8m/s 超（正常。閾値を上げるか除外する）。篩の脱線 0。
- ブランチ `develop`。今日の変更は未コミット（User が Windows 側で）。`Packages/manifest.json` に Recorder と Newtonsoft を明示追加。
- 球スキン表 `Assets/Data/BallSkins.asset`（106 行・サンプル仮貼り）、創作DB 名前は JSON/HUD に出る（2〜11 の 1 行目は不要になったが無害）。
- **別ボール 81 は 9×9(クック)（SemiPrimary `9x9`）に割当済み**（セッション末の User 指示）。`BallSkinTable.StreakOverrides` と asset の両方に反映。ただし 9x9 の Progress は `notProceeded` なので、公開基準（AGENTS.md 4.5）どおり名前は HUD/JSON に出ない。公開されたら自動で出る。テクスチャは User が Inspector で貼る。

## 2. 次にやること（優先順）

1. **MC 本計測と校正**: `LotoMonteCarlo` の静的フィールドを RunCommand で書き換えて 7 variant を n≈4 万（±0.005）で回し、`kuruun_params.json` の `variants.<name>.win` の幅を二分探索で詰める（1 万試行 ≈ 8 分）。条件は実機と同じ（rpm 10・`entryR 0.85`・`entryHeight 0.39`・減衰 0.05/0・`BallUtil.Machine`）。結果を DESIGN.md 2.1 節へ。最上段だけ接線投入（`entryTangential 1.4`）なので別途 1 回測る。
2. **塔の詰まりポケット**を潰す: 「stuck at level 1」の再現位置（樋の床 −0.71 付近・出口から 50° 前後）を `DerailWatch` に速度 0 継続の検知を足して特定し、録画（`Play + Record`）と突き合わせる。
3. **落下チャンネル**: 10 段の塔でハズレ球が 12m 自由落下してトレイに 14m/s で当たる。螺旋か段差にする（Blender）。
4. 配管の Blender 化（漏斗・シュート・筒・レール）と見た目（材質・照明）。カメラ: 篩の機械カメラは遠すぎて球が点。篩の球追従カット割りを検討。
5. `StreamingAssets/CreationsDB` 同期ツール、Linux ビルドの実機確認（未着手のまま）。

## 3. 触るときの注意（要点。詳細は AGENTS.md 5 章）

- 形を変えるのは `kuruun_params.json` → Blender で `gen_kuruun.py` 実行 → Unity `Assets/Refresh` → `Build Loto Scene`。FBX の角度は Unity = Blender + 180°（鏡映なし）。非対称メッシュは必ずレイキャストで実測。
- 生成メッシュ（ProcMesh）は片面。`Rotator` はワールド軸。回転体と静止体の隙間はゼロか 1.5d 超。
- **当たりを作るコードは書かない**（喉へ置く・引き寄せる）。詰まり救済は投入点へ戻すだけ。
- Play 中に Assets のスクリプトを保存しない（再コンパイルで録画が落ちる）。コンパイルエラーは Console の Type が Log で出ることがある（`error CS` でフィルタ）。
- Cowork サンドボックスの git は lock を消せない。コミットは User が Windows 側で。

## 4. 主要ノブ

| どこ | 何 | 既定 |
| --- | --- | --- |
| `kuruun_params.json` | bowl（rim 1.0 / cone 0.28 / ring 0.45〜0.65 / 6 穴×36°）・collector（top −0.20 / drop 0.16 / 樋 0.74〜0.94・fall 0.35・tilt 0.02 / 出口 Blender 0°）・variants.win・sieves（L: 3 穴×28°、U: 2 穴×40°） | — |
| `KuruunTower` | `bowlRpm` / `dropVelocity` / `angularDamping` / `funnelDamping` / `timeout` / `camFov` | 10 / 1.4 / 0.05 / 2.0 / 25 / 40 |
| `SieveMachine` | `dishRpm` / `spawnRadius` | 8 / rim−0.15 |
| `LotoMonteCarlo` | `variant` / `trials` / `parallel` / `bowlRpm` / `entryR` / `entryHeight` / `entryTangential` / `seed` | p10 / 2000 / 64 / 10 / 0.86 / 0.25 / 0 / 1 |
| `LotoSceneBuilder`（定数） | `TowerArcR` / `TowerPitch` / `TowerStagger` / `LowerLayers` / `UpperLayers` / `GutterR` / `GutterExitDepth` | 6.2 / 1.25 / 0.425 / 15 / 4 / 0.94 / 0.81 |
| `BallUtil` | `Bouncy`（球: 反発 0.6・摩擦 0.4）/ `Machine`（機械: 摩擦 0.3・反発 0.1） | — |
