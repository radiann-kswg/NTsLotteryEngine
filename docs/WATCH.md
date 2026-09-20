# WATCH.md — 観賞ビルド「NTsLotoWatch」要件定義（2026-09-20）

> NTsSphereChaser（RSC のコースターを Pi で流す試み）で分かった課題（重い・パッド無反応・スキン左右反転・原因不明が残る）を受け、
> **NTsLotteryEngine の中だけで完結する**観賞用ビルドを作る。抽選ビルド（`LotoScene` / `LotoBuild`）と MC には影響させない。
> 実装は `Assets/Scripts/Watch/`・`Assets/Scripts/Editor/Watch*.cs`・`BlenderSources/gen_coaster.py`。運用は `AGENTS.md` 11 章。

## 1. MUST（最小仕様）

| # | 要件 | 受け入れ条件 |
| --- | --- | --- |
| M1 | **スキン切替**: `Assets/Data/BallSkins.asset` の `texture != null` の行（2026-09-20 時点 8 球・将来 105/106 球）を ◀ ▶ で切り替えられる | PNG を `Assets/Textures/BallSkins/` に足して `Tools > NTsLoto > Build Ball View Scene` を回すだけで一覧に増える（コード変更なし） |
| M2 | **球面全体が見える動き**: 選んだ 1 球を物理で転がし、追従カメラで見られる。2 つの機械を切り替えられる — (a) **塔**: Loto のクルーン塔 1 本（`LotoSceneBuilder.BuildTower` を流用・当落は物理のまま）、(b) **コースター**: DinDon（神戸ハーバーランド umie のボールマシン）を思わせる螺旋樋＋リフトの新規メッシュ（`gen_coaster.py`。RSC の抽選機構造の簡略版） | 両モードで球が止まらずに周回し、落ちきったら同じ球を再投入する。**自動送り**（設定 On/Off・PlayerPrefs）が On なら落ちきるたびに次のスキンへ進む |
| M3 | **性能**: `RasPiOS_UnityConsole`（Pi 4B 4GB・box64・Mesa OpenGLCore）で 1280×720・**30FPS を維持** | `Player.log` に 10 秒ごとの `[Watch] fps avg/min` を出し、avg ≥ 30・min ≥ 20。物理で動く Rigidbody は常時 1 個、Pi 用 URP アセット（影なし・HDR なし・MSAA なし・深度/不透明テクスチャなし・ポスト無し）、`targetFrameRate=30`・vSync 0 |
| M4 | **音**: RSC の効果音（`Hit_1..4` の「コトッ」＋ `Lift_Loop`）を RSC と同じ鳴らし方・同じ既定音量で | Pi の HDMI から途切れず出る（OS 側 `1548480` で既定シンクは HDMI 済み）。BGM は入れない（RSC 側の PD 曲は今回は対象外） |
| M5 | **入力**: キーボード＋ゲームパッド。マウス不要。RSC の割当に揃える（LB/RB=球・X=機械切替・Y=音 All/SFX/Mute・Select=ヘルプ・Start/Esc=終了 or 戻る）。独自操作は A=自動送り On/Off | 旧 Input Manager（`activeInputHandler=0` のまま・パッケージ追加なし）。Pi では `ucon-run-app` の `SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS=1`（OS 側 `3c4a8ea`）が前提 |
| M6 | **NTsLotteryEngine 内で完結** | 新規 Unity プロジェクト・新規サブモジュールを作らない。RSC は音源（CC BY 4.0）を `Assets/Resources/SFX/` にコピーするだけ（`LICENSE.md` に表示） |

## 2. SHOULD（できれば）

- S1 タイトルシーン（`TitleScene`: Watch / Ball View / Quit）。Quit は `Application.Quit` → UnityConsole のランチャーへ戻る。
- S2 `BallViewScene` をビルドに含め、タイトル ⇄ Watch ⇄ BallView を双方向に遷移（B / Esc で戻る。BallView はパッド LB/RB で球切替・右スティックで回転）。
- S3 球の名前を HUD に出す（`Ball 93(Nintris)` / `Ball Binor`。創作DB `Name_EN` 1 行目・公開済みのみ。ビルド時に `StreamingAssets/CreationsDB/` へ公開レコードだけ書き出す＝git 管轄外）。

## 3. やらないこと（YAGNI）

- 複数球の同時投入（Pi で球数に比例して重くなる。要るなら MUST を満たしたあと）。
- Input System への移行（パッケージ追加・エディタ再起動が要る。旧 Input Manager で足りる）。
- 遅延ロード（全スキン 2048×1024 を一度に読む。106 球で起動が遅ければそのとき）。
- Pi で見つかったスキン左右反転の原因追究（受け入れ条件: BallView の既定姿勢を Windows と見比べて報告する）。

## 4. 検証手順

1. エディタ: `Tools > NTsLoto > Build Watch Scene`（冪等）→ Play で両モード・自動送り・音。`recompile_status` の errors 0。
2. `Tools > NTsLoto > Build Watch Linux x64 (RPi)` → `Builds/Watch/NTsLotoWatch.x86_64`（OpenGLCore 固定・Mono）。
3. `F:\UnityGames\NTsLotoWatch\` にビルド一式＋`game.json`＋`icon.png` を置く（`*_BackUpThisFolder_ButDontShipItWithYourGame` は除外）→ Pi に挿す → User が起動して FPS ログ・音・パッドを報告。

## 5. 実機での所見（2026-09-20・Pi 4B / RasPiOS_UnityConsole）

USB カセットで導入した版を仮想パッド（uinput の Xbox360 相当）で 1 入力ずつ検証した結果。

| 項目 | 結果 |
| --- | --- |
| ボタン A / B / X / Y / LB / RB / Select / Start | 効く（`ucon-run-app` の `SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS=1` が前提） |
| 左スティック | 効く（既定の `Horizontal` / `Vertical`） |
| 十字キー | **効かなかった** → `InputManager` に `WatchDPadX` / `WatchDPadY` を足して対応（`WatchInput` が両方を読む） |
| FPS（M3 の受け入れ条件 avg ≥ 30 / min ≥ 20） | **avg 5.3 / min 3.2（Tower・1280×720・70 秒）** … 未達。`temp=45.7'C` / `throttled=0x0` なので熱でも電源でもない |

- Linux(SDL) の十字キーは軸ではなく**ハット**で来る。Unity では *7th axis / 8th axis*（`InputManager` の `axis: 6` / `7`）に現れ、
  既定の `Horizontal` / `Vertical`（スティック）には乗らない。
- ハットの縦は Unity の生値が**下で +1**（evdev と同じ向き）。既定の `Vertical`（スティック）と同じ向きにそろえるには
  `WatchDPadY` は `invert: 1` が要る。`invert: 0` にすると十字キー↓でカーソルが上がる（両方を実機で試して確認）。
- 画面写真 1 枚で向きを判断しないこと。項目 3 つのメニューは**巻き戻る**ので、押下が 1 回落ちただけで逆向きに見える。
  「押さない状態の写真を続けて 3 枚撮る」（`retest-dpad2.py`）と、二重発火と取りこぼしを切り分けられる。
- **FPS が 5 のままだと軸入力は取りこぼす**。軸（十字キー・スティック）は毎フレームのポーリングなので、
  1 フレーム 200ms の間に押して離すと拾われない。ボタンはイベントなので落ちにくい。
  「パッドが反応しない」という体感の半分はここから来ている。M3 を満たすまでは操作感も直らない。

### 実機試験の道具（`WSLSettings/scripts`）

| スクリプト | 役割 |
| --- | --- |
| `deploy-lotowatch.sh` | `C:\Users\Public\unitycon\UnityGames\NTsLotoWatch` を rsync → `ucon-install`（カセットと同じ経路） |
| `verify-lotowatch-input.py` | 仮想パッドで 1 入力ずつ押し、各段の画面を `/tmp/v-*.png` に回収 |
| `lotowatch-fps-test.py` | Watch を 70 秒回して `[Watch] fps` を採る |
| `retest-dpad.py` | 十字キーの向きだけを見る最小の試験 |

- ランチャーは「一覧 → 詳細パネル（起動 / バックアップ / 削除 / 戻る）」の 2 段。前の試験の続きだと詳細パネルに居るので、
  試験は毎回 `systemctl restart unitycon-launcher` から始める（固まったときの復帰も同じ）。
- `pkill -f` でゲームを畳むときは**先頭アンカー必須**（`^/var/lib/unityconsole/apps/<App>/…`）。無いと ssh 自身の `bash -c` を殺して無出力 exit 1 になる。
