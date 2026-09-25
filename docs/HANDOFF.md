# HANDOFF.md — 次セッションへの引継ぎ（2026-09-20 時点・制作その５＝観賞ビルド。抽選側は 2026-09-04 のまま）

> 読む順: `AGENTS.md`（運用・罠）→ `docs/DESIGN.md`（仕様・機構）→ 本ファイル（いまの状態と次にやること）。

## 環境追記（2026-09-25）

Codexの作業ブランチは`develop-codex`。運用の正本は`AGENTS.md` 2章・3.1節。
Blender Lab MCP（5.2.2 LTS）とUnity公式リレーMCP（6000.6.2f1）の両方で生成・動作検証済み。
`unityMCP`側はインスタンス0件だったため、既存の`C:\Users\s-chi\.unity\relay\relay_win.exe --mcp --project-path <Codexチェックアウト>`へ接続した。
Claude用チェックアウトのエディタも開いているので、必ずプロジェクトパスを指定する。設定ファイルは変更していない。

### ガムボール観賞モード

- 2つ目のモードをコアフォルダ風タンク付きのスパイラルガムボール機に変更。原本は`BlenderSources/gen_gumball.py` / `Gumball.blend`。再生成手順・軽量化の内訳・制限は`docs/WATCH.md` 4章。
- 既存の螺旋樋・塔・入力割当・PlayerPrefsを維持。充填球123個は9スキン別の描画専用メッシュに結合、排出する物理球は1個。筐体2,942三角形、充填球合計31,980三角形。
- `Tools > NTsLoto > Validate Gumball`: PASS。入口の床y=1.492、上向き法線を確認、排出口から受け皿まで約14.7秒（待機3秒込み）。追加した排出管は下端y=1.64が必要（1.57では球を止める）。
- Playで自動送り18周完走、塔との切替・排出待機中の中断復帰後も自動送りOffで完走し、合計24周・FaultCount=0。観賞Play検証終了時のConsoleはError/Warning=0。全9スキンの充填、アクティブRigidbody=1も確認。
- `Capture Gumball Preview`で`docs/captures/preview_gumball.png`を生成し、READMEに掲載。Linux x64/Mono/OpenGLCoreビルドはSucceeded（133MB、`Builds/Watch/`）。ビルド時に既存のAI Inference/Sentisシェーダー・Pipelineランタイム設定なし・衝突メッシュ事前ベイクの警告あり（排出管Chuteも事前ベイク警告の対象、現バージョンはビルド時に自動ベイク）。Pi 4B/5実機でのFPS・音・パッドの再測定と配布先への転送は未実施。
- 開始時からの`ProjectSettings/UnityConnectSettings.asset`の変更は今回の対象外。

## 0. 制作その４でわかったこと（最重要）

- **当落を決めているのは「球がコレクタ天端を割った瞬間の方位」**。実機 407 段のログで方位から当落を 99.0% 再現（`Output/drop_azimuth.csv`）。
- **実機の落下方位は一様ではない**（120〜150° と 300° に山、0〜90° が谷）。MC の校正値は「方位一様の平均」なので、
  当たり扇形が 1 本だとその偏りをそのまま拾う。→ **対策: 当たり扇形を円周に等間隔で散らした**（`kuruun_params.json`）。
  **1 本 21° の下限**（内縁で楔に挟まる。18.5°×2 は p=0・timeout 11%）があるので、合計角の小さい **p10 と p6of88 だけは分割できず 1 本のまま**。

  | variant（塔） | 本数 修正前→後 | 実測 p 修正前 | **修正後** | 95% CI | 目標＝校正値 |
  | --- | --- | --- | --- | --- | --- |
  | p10（1・0） | 1 → **1** | 0.169 | 0.144 | [0.096, 0.210] | 0.100 △ |
  | p6of88（10） | 1 → **1** | 0.123 | 0.108 | [0.053, 0.206] | 0.068 △ |
  | p18（81） | 1 → **3** | 0.261 | **0.177** | [0.109, 0.276] | 0.180 ✓ |
  | p32（64） | 2 → **5** | 0.226 | 0.235 | [0.158, 0.336] | 0.320 ✓ |
  | p50（2） | 2 → **5** | 0.539 | 0.430 | [0.343, 0.522] | 0.500 ✓ |
  | p53（33） | 2 → **5** | 0.536 | 0.562 | [0.481, 0.640] | 0.530 ✓ |

  目標からの乖離は **χ²(6) = 19.1（p=0.004）→ 10.4（p=0.11）** に半減し、6 塔とも CI が目標を含むようになった。
  ただし **p10 / p6of88 はメッシュ不変なので 130 回ぶんを合算すると 0.156 [0.119,0.202]／0.115 [0.071,0.182] でまだ目標の外**。
- **懸念だった「投入方位が固定だから当落が決まる」は実機では起きていなかった**（喉から落ちる球がばらけて Δ の中央値 105〜115°・分布が広い）。
  段ごと（最上段 135° / 奇数段 270° / 偶数段 90°）の差は概ね 95% CI 内。
- 詳細な表・方位ヒスト・残りの手は `docs/DESIGN.md` 2.2（校正値は 2.1）。
- ボールテクスチャの保存場所を確定（`AGENTS.md` 8章）: PNG は `Assets/Textures/BallSkins/BallTex_NTS-{Num_Badge}.png`（git 管轄内・CC BY-NC 4.0）、
  編集用 PSD は `E:\Dropbox\Creative Cloud Files\ナンバーテールズ\LotteryBallKit\`（git 管轄外）。

## 1. いまの状態

- **7 塔 6 variant のコレクタを MC で校正済み**（有効数字 2 桁）。扇形の幅は `BlenderSources/kuruun_params.json` の `variants.<name>.win`、実測値は `docs/DESIGN.md` 2.1 節と `Output/mc_<variant>.json`。MC 治具の 2 つの穴（1 試行目のボウル中心落下・境界角の 3.75° 量子化）とコレクタの内縁 0.02 を直した（AGENTS.md 罠16〜18）。
- **脱線ゼロ**: 最終形で全経路 Play ×3（`Recordings/20260903-203225 / 203719 / 204820.mp4`）と塔だけの Play ×2 で `DerailWatch` 警告 0・stuck 再投入 0。直した原因は段間隔（1.25 → 1.45）・投入速度の座標系・シュートの向き・穴リングの桟（AGENTS.md 罠19〜21・23）。`DerailWatch` は自由落下（下向き 18m/s 未満）を除外し、塔の球がトレイ以外で地面に居る「escaped」を足した。最上段の投入は下段と同じ静止落下（罠24）。
- **篩のカメラ**: 抽選中の篩の正面 1.9m・FOV 40 で、高さは最下の球に追従（`SieveMachine.Follow`・`camAhead 0.25`・`camLerp 3`）。ガラスは alpha 0.10・smoothness 0.15（近接だと白飛びした）。
- **篩の公平性**: 2〜11 は番号順に生成すると最後に生成した球（11）が 6 回連続で最初に着いた（皿の桟が平らで静止した球が皿と一緒に回り続け、動いている球だけが落ちる）。皿の桟を屋根形（`sieves.*.ring_roof` 0.02）にし、投入位置と生成順を毎回シャッフル（AGENTS.md 罠23・25）。直後の Play は 7（slot 8）が勝ち。外周の slot が有利な傾向は残るかもしれないので、次の数回の結果を見ること。
- ブランチ `develop`。今日の変更は未コミット（User が Windows 側で）。
- 別ボール 81 は 9×9(クック)（Progress notProceeded のため名前は出ない）。テクスチャは User が Inspector で貼る。

## 1.5 観賞ビルド NTsLotoWatch（2026-09-20・制作その５）

- `docs/WATCH.md` の MUST をエディタで確認済み（両モードとも球が周回・自動送り・FPS ログ）。Linux ビルド成功（132MB）→ USB カセット `F:\UnityGames\NTsLotoWatch\` に配置済み。**Pi 実機での確認は未実施**（User が挿して起動 → FPS・音・パッドを報告する段取り）。
- 未コミットのまま残す差分: `ProjectSettings.asset` の `organizationId` / `projectName`（Unity の自動書き換え・User 判断）と `UnityConnectSettings.asset`、`BallViewScene.unity` の fileID 振り直し。Watch ビルドが変える `defaultScreen 1280×720` と Linux の `OpenGLCore` はビルドのたびに `WatchBuild` が当て直すのでコミット不要。
- 次の手: Pi で avg 30 に届かなければ `Pi_RPAsset` の `renderScale` を 0.8 へ／塔モードのカメラ（外周 1.9m から中を覗く。ボウルの壁越しになる区間がある）の見直し／Pi でスキンが左右反転する件（NTsSphereChaser で未解決）を BallView の既定姿勢で見比べる。

## 2. 次にやること（優先順）

1. **p10 / p6of88 の上振れを直す**（`docs/DESIGN.md` 2.2 (f)。扇形の分割は 1 本 21° の下限に阻まれる）:
   - (b) コレクタの当たり／ハズレ円盤だけをボウルと違う回転数で回す（樋は静止のまま分離）。本数に関係なく効く根治策。`gen_kuruun.py` で円盤と樋を別オブジェクトにする。
   - (c) 内縁 `r_in` を 0.36 → 0.45 に広げて下限を 17° 程度に下げ、p10 を 2 本に割る。漏斗・落差・全 variant の再校正が要る。
   - (d) 扇形境目の放射壁を r ≈ 0.46 で止める（詰まりは内縁で起きている）。未検証。
   **どれをやっても MC だけでなく実機 Play ×65 で測り直すこと**（`LotoPlayLoop.Start(65, "Output/verifyN")`。1 回 ≈ 24 秒・全部で約 26 分）。
   p32/p50/p53 の点推定のばらつきも n≈100 では判定できないので、回数を増やすなら同時に。
2. **詰まりの再発を潰す**: 65 回中 1 回、`[Tower6_Ball81_x4] ball 81 stuck at level 0 (y=0.05)` の再投入警告。y=0.05 ＝ 球が下まで落ちている（トリガーを踏まずに逃げた）。録画で同時刻を切り出す（`ffmpeg -ss T -t 1 -vf fps=15`）。
3. 篩側も同じ観点で 1 度測る（2〜11 の勝者のばらつき。`Tools > NTsLoto > Play + Record` → `[SieveU] spawn/arrived`）。
4. 配管の Blender 化（漏斗・シュート・筒・チャンネル・レール）と見た目（材質・照明）。塔は 11 段 ≈ 16m なので全景カメラは要再検討。
5. `StreamingAssets/CreationsDB` 同期ツール、Linux ビルドの実機確認（未着手）。
6. ボールテクスチャの実装（進行中・2026-09-19 時点 **8 / 106 球**: ロト 4・6・50・58・63・85・93、別ボール 2（バイナ `2B`））: `Assets/Textures/BallSkins/` に `BallTex_NTS-{Num_Badge}.png` を置いたら `Tools > NTsLoto > Build Ball View Scene`（空き行へ自動割当・冪等）→ `Tools > NTsLoto > Capture Ball Skins`（BallViewScene を開いた状態で。正面・俯瞰の静止画＋回転 GIF の撮影と README 収録表（区分ごとに折りたたみ）の書き戻し）。Inspector での手貼りは個別上書きのときだけ。ファイル名は `Num_Badge`（別ボールは `Num` と違う: `2B` / `10D` / `64XP`）。規約は同フォルダの `README.md`、運用は `AGENTS.md` 4 章 7 と 8 章。

## 3. 触るときの注意（要点。詳細は AGENTS.md 5 章）

- 形を変えるのは `kuruun_params.json` → Blender で `gen_kuruun.py` 実行（Blender MCP: `REPO=...; exec(open(...).read())`）→ Unity `AssetDatabase.Refresh` → `Build Loto Scene`。FBX の角度は Unity = Blender + 180°（鏡映なし）。
- **コレクタ・段間隔・減衰・材質を変えたら MC を回し直す**（`LotoMonteCarlo.RunBatch(new[]{...})` を RunCommand から。10 万試行 ≈ 75 秒。エディタが非アクティブだと update が間引かれるので `stepsPerUpdate` を 2500 に）。
- **当たりを作るコードは書かない**（喉へ置く・引き寄せる）。詰まり救済は投入点へ戻すだけ。
- Play 中に Assets のスクリプトを保存しない（再コンパイルで録画が落ちる）。MC 中もスクリプトを保存しない（ドメインリロードで MC が死に `Physics.simulationMode` が Script のまま残る。残ったら `Physics.simulationMode = SimulationMode.FixedUpdate` に戻す）。
- Cowork サンドボックスの git は lock を消せない。書き込みは親フォルダの `scripts/g.sh NTsLotteryEngine <git args>` 経由（lock を `.git/stale-locks/` へ退避）。退避先の掃除は `scripts/clean-git-locks.ps1`。

## 4. 主要ノブ

| どこ | 何 | 既定 |
| --- | --- | --- |
| `kuruun_params.json` | bowl（rim 1.0 / cone 0.28 / ring 0.45〜0.65 / 6 穴×36° / ring_roof 0.012）・collector（top −0.20 / drop 0.16 / rim_h 0.12 / 樋 0.74〜0.94・fall 0.35・tilt 0.02 / 出口 Blender 0°）・variants.win（校正済み・落差 0.59: p10 37.0 / p6of88 25.1 / p18 65.2 / p32 2×57.3 / p50 2×91.0 / p53 2×96.3）・sieves（L: 3 穴×28°、U: 2 穴×40°、ring_roof 0.02） | — |
| `KuruunTower` | `bowlRpm` / `dropVelocity`（ローカル。既定ゼロ = 下段と同じ静止落下） / `angularDamping` / `funnelDamping` / `timeout` / `camFov` | 10 / 0 / 0.05 / 2.0 / 25 / 40 |
| `SieveMachine` | `dishRpm` / `spawnRadius` / `camAhead` / `camFov` / `camLerp` | 8 / rim−0.15 / 0.25 / 40 / 3 |
| `LotoMonteCarlo` | `variant` / `trials` / `parallel` / `bowlRpm` / `entryR` / `entryHeight` / `entryTangential` / **`entryAzimuth`**（NaN=ランダム） / `seed` / `stepsPerUpdate`。`RunBatch(variants)` と **`RunGrid(variants, azimuths)`** | p10 / 2000 / 64 / 10 / 0.85 / 0.59 / 0 / NaN / 1 / 2500 |
| `LotoPlayLoop`（Editor） | `Start(runs, dir, src)` で Play を繰り返し、結果 JSON を `dir` へ退避。`Tools > NTsLoto > Play Loop > Cancel` で中止 | — / `Output/verify` / `result.json` |
| `LotoDirector` | `skipSieves`（塔だけ回す） / `speed`（`Time.timeScale`） | false / 1 |
| `LotoSceneBuilder`（定数） | `TowerArcR` / `TowerPitch` / `TowerStagger` / `LowerLayers` / `UpperLayers` / `GutterR` / `GutterExitDepth` / `ChW` | 6.2 / 1.45 / 0.425 / 15 / 4 / 0.94 / 0.81 / 1.24 |
| `DerailWatch` | `maxSpeed`（水平・上向き） / `maxFallSpeed` / `minY` / `maxR` | 8 / 18 / −0.5 / 12 |
| `BallUtil` | `Bouncy`（球: 反発 0.6・摩擦 0.4）/ `Machine`（機械: 摩擦 0.3・反発 0.1） | — |
