# DESIGN.md — NTsLotoEngine 抽選仕様と機構

## 1. コンセプト

ナンバーテールズのコンテンツ用ボール抽選機。**当選は物理が決める**（2026-09-03 User 指示。RNG で先に決めて誘導する旧方式は「作為的」なので廃止）。

- 2〜11・12〜99 は **篩型ロトマシーン**: 穴あきの回転皿を縦に重ね（4 層／15 層）、球は穴を見つけるたびに下へ落ちる。最下層の受け口に**到達した順**が抽選順。
- 別ボール 7 台は **縦連クルーン**: 各段 = 回転ボウル（穴は一様）＋静止コレクタ（当たり扇形は軸へ／ハズレ扇形は外周の樋へ）。当選率はコレクタの当たり扇形の角度比で作り、**モンテカルロ（`Tools > NTsLoto > Monte Carlo`）で実測して有効数字 2 桁を保証**する。
- 抽選機のモデルは **Blender で生成**（`BlenderSources/gen_kuruun.py` + `kuruun_params.json` → `Assets/Models/*.fbx`）。Unity 側（`LotoSceneBuilder`）は配置と配管だけ。RSC と同じ方針。
- 1 台ずつ順番に回し、固定カメラを切り替える（動画向け）。

## 2. 抽選仕様（正本: `Assets/Scripts/LotoRules.cs`）

| # | 抽選 | 内容 | 目標当選率 | 段数 | コレクタ variant |
| --- | --- | --- | --- | --- | --- |
| A | 上段の篩（4 層） | 2〜11 の 10 球から最下層到達の 1 球 | 各 1/10（物理） | — | — |
| B | 下段の篩（15 層） | 12〜99 の 88 球から到達順に 6 球 | 各 6/88（物理） | — | — |
| C1 | 別ボール **1** | A と同じ当選率 | 0.10 | 1 | p10 |
| C2 | 別ボール **10** | B と同じ当選率 | 6/88 ≈ 0.068 | 1 | p6of88 |
| C3 | 別ボール **0** | 連続抽選 | 0.10 | 3 | p10 |
| C4 | 別ボール **2** | 連続抽選 | 0.50 | 10 | p50 |
| C5 | 別ボール **33** | 連続抽選 | 0.53（2^(−10/11) を 2 桁に） | 11 | p53 |
| C6 | 別ボール **64** | 連続抽選 | 0.32（2^(−5/3)） | 6 | p32 |
| C7 | 別ボール **81** | 連続抽選 | 0.18（2^(−5/2)） | 4 | p18 |

- 連続抽選は「どこかで落選したら即清算」。結果は **連続当選回数**（0 = 初回落選、max = 完走）。
- 目標値は形状で近似し、実測 p（Wilson 95% 区間）が目標の有効数字 2 桁に収まるまで扇形の角度を詰める。有効数字 2 桁の範囲なら比率丸め（例 6/88 → 0.068）も可（User 合意 2026-09-03）。実測結果は `Output/mc_<variant>.json` と本書 2.1 節に記録する。
- 出力 JSON（`LotoResult`）: `seed`（投入ジッタの種。結果の再現は保証しない）, `drawnAt`, `single`, `six`（到達順）, `sixSorted`, `streaks[{ball,wins,max,targetP,nameJP,nameEN}]`。

### 2.1 実測記録（MC）

| variant | 目標 | 実測 p | 95% CI | n | 条件 |
| --- | --- | --- | --- | --- | --- |
| p50 | 0.50 | 0.528 | [0.507, 0.550] | 2048 | 現行コレクタ（win 2×90°）・rpm 10・垂直投入 r0.85 h0.39・`Machine` 摩擦 0.3。当たり側に +0.03 の偏り → 扇形を数度狭める。n≈4 万で再測すること |
| その他 6 variant | — | 未計測 | — | — | 次セッションで本計測 |

## 3. 機構（正本: `Assets/Scripts/Editor/LotoSceneBuilder.cs`・寸法は `BlenderSources/kuruun_params.json`）

単位 m。球は LotteryBallKit `NumberBall`（径 0.1 = d）。FBX の角度規約: **Unity 角 = Blender θ + 180°**（レイキャスト実測）。

### 3.1 篩型ロトマシーン ×2（`SieveMachine`）

```
  Spawn（最上層の 0.45 上・螺旋にばらまく）
 ╲______╱  Dish 0  … 回転皿（Sieve_Dish_L: φ1.4・縁高 0.22・穴リング r0.26〜0.42 に 4 穴×32°／Sieve_Dish_U: φ0.9・3 穴×44°）
 ╲______╱  Dish 1     層ごとに回転方向を交互（±8rpm）、穴の位相は 37° ずつずらす
   ...               層間隔 0.34（L）／0.32（U）。透明筒（皿の縁との隙間 0.03 ＜ d/2）
 ╲______╱  Dish 14
   ╲____╱  Funnel（喉 2.4d）→ Exit トリガー（到達 = 抽選）→ Gate（抽選が終わったら閉じる）→ 8° 下りシュート → 結果レール
```

- 下段は原点、上段は x=−1.7。両方とも結果レール（z=−1.10・+X 側 3° 下り）へ落とし、7 球が到達順に並ぶ。
- `SieveMachine.DrawNext` は受け口に着いた順のキューから 1 球ずつ返す（抽選の合間に着いた球も落とさない）。

### 3.2 別ボールの縦連クルーン塔 ×7（`KuruunTower`。ロトマシーンを中心とする半径 6.2 の半円弧に扇状配置・正面を機械に向ける）

並び（弧の左→右）: 1(x1)・0(x3)・64(x6)・**33(x11)**・2(x10)・81(x4)・10(x1)。

```
   ↓ 投入（最上段のみ: コーン面 r=0.85・角度 135° の 0.30 上へ接線速度 1.4）
 ┌──────────┐  Kuruun_Bowl（回転 10rpm）: 縁 r1.0・壁高 0.56・コーン 27°（r1.0,h0.28 → r0.65,h0）・穴リング r0.45〜0.65 に 6 穴×36°・中央ドーム h0.22
  ╲   ○   ╱   球はコーンを螺旋で下り、穴からコレクタへ落ちる（落下角は回転で一様化）
   ╲_____╱
  ┌‾‾‾‾‾‾‾‾┐  Kuruun_Collector_<variant>（静止）: 天端 −0.20。当たり扇形 = 内側へ 23° 下り → 内縁 r0.36 から中央へ落下 → Win トリガー（−0.44）→ 漏斗（喉 2.4d・−0.70）→ 次段のコーン面（x を ±0.425 交互にずらす）
  │  樋   │   ハズレ扇形 = 外側へ下り → 樋（r0.74〜0.94・幅 2d・出口へ 0.25/半周の勾配）→ 正面の出口（開口 20°）→ シュート → 落下チャンネル → ハズレトレイ
```

- 段間隔 1.25（喉 −0.70 → 次段コーン面 −1.09）。11 段で約 14 m。最下段を当たりで抜けた球は喉の真下の完走トレイへ。
- 球の減衰は **素の物理**（角 0.05・線 0。MC と同じ）。漏斗内だけ角減衰 2.0（喉で周回し続けない。当たり外れ確定後なので確率に影響しない）。
- 詰まったら（25s）その段の投入点へ戻してやり直す。**喉へ置いて当たりを作ることは絶対にしない**。

### 3.3 進行（`LotoDirector`）

1. カメラ `Cam_Machine`。上段・下段の篩を回し、上段の到達 1 球 → 下段の到達 6 球（`pauseBetween` 1.5s）。終わったら Gate を閉じる。
2. 別ボール 1 → 10 → 0 → 2 → 33 → 64 → 81 の順に塔へ（カメラは球に追従・FOV 40）。
3. `Cam_Overview` に切り替え、JSON を書く。`-quit` 指定なら 3 秒後に終了。

起動引数: `-seed N`（未指定は時刻）／`-out path`（既定 `result.json`）／`-speed x`（`Time.timeScale`）／`-quit`。

### 3.4 球のスキンと創作DB

- `Assets/Data/BallSkins.asset`（`BallSkinTable`）: 全 106 球（ロト 1〜99 ＝ `Drum`、別ボール 7 ＝ `Streak`）の 1 行ずつ。行 = テクスチャ・tint・創作DB の `db`/`dbNum`。`Build Loto Scene` が足りない行だけ足す（既存行の手貼りは保持）。テクスチャが空の行は `defaultTexture`（いまは LotteryBallKit の `BallSkins_Sample`＝仮）。正式なテクスチャは User が Inspector で 1 球ずつ差し替える。
- 別ボールのキャラ対応（User 指定 2026-09-03。`BallSkinTable.StreakOverrides`）: 0→`000`(チトセ)／10→`10-alt`(ディケ)／2→`2-alt`(バイナ)／33→SemiPrimary `3x11`(トレッド)／64→SemiPrimary `64-sxp`(ゼフィア)／81→SemiPrimary `9x9`(クック。Progress notProceeded のため公開まで名前は空)。1 とロトの球は Primary の同番号（2→2(ツグ)、10→10(ミツル)）。
- `CreationsDb`（`Assets/Scripts/CreationsDb.cs`・Newtonsoft）: NTsWallpaperEngine と同じ探索順（環境変数 `NTSLOTO_CREATIONSDB` → `StreamingAssets/CreationsDB` → サブモジュール直読み）と公開基準（`Progress` が released / released(beta) / stillTentative / unreleased）。結果 JSON に `singleName` / `sixNames[]` / `streaks[].nameJP,nameEN`、HUD に英名（`Ball  0 Thouser: 1/3 WIN`）。StreamingAssets への同期ツールは未作成（ビルドではサブモジュールが無いので名前は空になる）。

## 4. 未着手・今後

- **配管の Blender 化**: 漏斗・シュート・チャンネル・透明筒・レールはまだ ProcMesh／Box。機構が固まったら `gen_kuruun.py` に足す。
- **MC の本計測**: 7 variant × n≈4 万（±0.005）。1 万試行 ≈ 8 分（並列 64）。塔の実機（回転ボウル＋投入条件）と MC の条件が同じであることを 1 度は実機の到達統計で突き合わせる。
- 動画化: エディタでは `Tools > NTsLoto > Play + Record`（Unity Recorder・`Recordings/*.mp4`）。ビルドでは Bot 側で画面録画（RPi なら `ffmpeg -f x11grab`）。
- 球の正式テクスチャ（`BallUV_Template` に合わせた作画。NC 素材のため LICENSE.md の区分に注意）と `StreamingAssets/CreationsDB` 同期ツール。
- HUD は OnGUI＋PenchantManufacture（CJK 未収録なので文字列は英数字のみ。RSC 罠53）。TMP 化は必要になったら。
- 物理パラメータ（rpm・引き寄せ・timeout）は実機で回して調整する。全部 Inspector のノブ。
