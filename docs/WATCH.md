# WATCH.md — 観賞ビルド「NTsLotoWatch」要件定義（2026-09-25）

> NTsSphereChaser（RSC のコースターを Pi で流す試み）で分かった課題（重い・パッド無反応・スキン左右反転・原因不明が残る）を受け、
> **NTsLotteryEngine の中だけで完結する**観賞用ビルドを作る。抽選ビルド（`LotoScene` / `LotoBuild`）と MC には影響させない。
> 実装は `Assets/Scripts/Watch/`・`Assets/Scripts/Editor/Watch*.cs`・`BlenderSources/gen_coaster.py` / `gen_gumball.py`。運用は `AGENTS.md` 7.5 章。

## 1. MUST（最小仕様）

| # | 要件 | 受け入れ条件 |
| --- | --- | --- |
| M1 | **スキン切替**: `Assets/Data/BallSkins.asset` の `texture != null` の行（2026-09-20 時点 8 球・将来 105/106 球）を ◀ ▶ で切り替えられる | PNG を `Assets/Textures/BallSkins/` に足して `Tools > NTsLoto > Build Ball View Scene` を回すだけで一覧に増える（コード変更なし） |
| M2 | **球面全体が見える動き**: 選んだ1球を物理で転がし、追従カメラで見る。(a) **塔**: 既存のクルーン塔、(b) **ガムボール機**: 登録スキン入りのコアフォルダ風タンクから1球ずつ排出し、既存の螺旋樋を下る（`gen_gumball.py` + `gen_coaster.py`） | 両モードで周回する。自動送りOnなら受け皿到達後に次のスキンへ。Offなら同じ球。排出待機中は筐体全景、転がり中は追従カメラ |
| M3 | **性能**: `RasPiOS_UnityConsole`（Pi 4B 4GB・box64・Mesa OpenGLCore）で 1280×720・**30FPS を維持** | `Player.log` に 10 秒ごとの `[Watch] fps avg/min` を出し、avg ≥ 30・min ≥ 20。物理で動く Rigidbody は常時 1 個、Pi 用 URP アセット（影なし・HDR なし・MSAA なし・深度/不透明テクスチャなし・ポスト無し）、`targetFrameRate=30`・vSync 0 |
| M4 | **音**: RSCの衝突音`Hit_1..4`を従来と同じ鳴らし方・音量で。ガムボール機にはリフトがないので常時`Lift_Loop`は鳴らさない | PiのHDMIから途切れず出る（OS側`1548480`で既定シンクはHDMI済み）。BGMは入れない |
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

1. Blender MCP: `REPO`をチェックアウト絶対パスに設定し、`BlenderSources/gen_gumball.py`を実行。専用の`Gumball`シーンだけを再生成し、`Gumball.blend`と2つのFBXを出力する。既存の`Coaster_Helix.fbx`は変更不要。
2. エディタ: `Tools > NTsLoto > Build Watch Scene`（冪等）→ `Validate Gumball`（Editモード）→ Playで両モード・自動送り・音を確認。`Capture Gumball Preview`でREADME画像を更新。ConsoleのError/Warningに加え`error CS`も確認。
3. `Tools > NTsLoto > Build Watch Linux x64 (RPi)` → `Builds/Watch/NTsLotoWatch.x86_64`（OpenGLCore固定・Mono、Pi 4B/5ではbox64経由）。
4. `F:\UnityGames\NTsLotoWatch\` にビルド一式＋`game.json`＋`icon.png` を置く（`*_BackUpThisFolder_ButDontShipItWithYourGame` は除外）→ Pi に挿す → User が起動してFPSログ・音・パッドを報告。

### ガムボール機の構成（2026-09-25）

- 形状参考: [大型スパイラルガムボールマシン](https://jp.made-in-china.com/co_cnfordga/product_Large-Spiral-Toy-Vending-Machine_eoggunsuy.html)。商品画像・メッシュは取り込まない。コアフォルダの丸い体・獣耳・尻尾は創作DBの既存画像を形状参考にしたもの。新しいキャラクター設定は付与しない。
- `Gumball_Cabinet.fbx`: 2,942三角形・6メッシュ。筐体、金色の縁、目、透明タンク、排出管、スライドゲート。衝突判定は排出管・既存樋・受け皿だけ。高い透明外筒を省き、重なり描画を抑える。
- `Gumball_FillBall.fbx`: LotteryBallKitの球本体をUV維持で260三角形へ簡略化。123球を起動時にスキン別結合（現在9描画メッシュ）、Simple Lit、影なし、Collider/Rigidbodyなし。合計31,980三角形。`Read/Write`はビルダーが有効化し、結合後のCPU側頂点データは解放する。
- `BallSkins.asset`の`texture != null`の全行を順に繰り返し充填。現在9スキン、既存の106行まで同時表示できる。将来123スキンを超える場合は充填配置を拡張する。キャラクター名の公開制限は従来の`CreationsDb`に従う。
- 充填球は観賞用の固定表示。群全体の物理や在庫の減少はシミュレーションしない。選択球1個を不透明なカラー内に戻し、排出管へ送ってゲートを開けた後は重力で転がす。周回中の生成・破棄はない。
- 既存の`Machine.Coaster=1`とPlayerPrefsは維持。表示名のみ`Gumball`へ。待機2秒・ゲート開放0.35秒・入口初速0.4m/s・減衰0.5・受け皿待機3秒。外側リフトと常時リフト音は撤去し、衝突音を維持。
- `Validate Gumball`はインポートした排出管の座標、樋入口の高さ・法線、メッシュ上限、実際の球の受け皿到達を検証する。排出管下端はy=1.64（1.57だと樋上の球を止める）。
- Piの30FPS目標は実機で再計測が必要。過去の測定（下記）は旧コースターの値であり、この筐体の測定値ではない。

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

### FPS の切り分け（2026-09-20・実測）

`[Watch] fps` と `/proc/<pid>/fdinfo` の `drm-engine-render`（V3D の稼働率）、`top` を突き合わせた。

| 条件 | avg / min | GPU render | CPU |
| --- | --- | --- | --- |
| 1280×720・塔 | 5.1 / 3.2 | 99.9% | 1 コアの 3 割 |
| 1280×720・コースター | 8.1 / 5.4 | 99.9% | 同上 |
| 640×360（窓）・塔 | 18.9 / 11.3 | 97.4% | 同上 |

- **CPU は暇で GPU(V3D) が振り切れている＝フィルレート律速**。画素数を 1/4 にすると fps は 3.7 倍。box64 や物理は犯人ではない。
- 熱・電源は無関係（`temp≈44'C` / `throttled=0x0` / arm 1.8GHz・v3d 500MHz で張り付き）。
- 対策として Pi 用 URP アセットの `m_RenderScale` を **0.5** に（出力は 1280×720 のまま、内部 640×360 で描いて引き伸ばす）。実測:

| | 対策前 | ① renderScale 0.5 | ②＋透明の裏面カリング |
| --- | --- | --- | --- |
| 塔 | 5.1 / 3.2 | 17.3 / 11.1 | **21.6〜30.0 / 14.0〜30.0** |
| コースター | 8.1 / 5.4 | 26.5 / 19.2 | **23.6〜30.0 / 16.4〜30.0** |

②は `LotoSceneBuilder.Mat` の透明マテリアルを `_Cull Back` にしたもの（不透明は生成メッシュの巻き方向に依存しないので `Cull Off` のまま）。
透明を両面描くと同じ画素を 2 回ブレンドする。ガラスは筒・箱・樋なので裏面を落としても中の球は見える（実機の画面で確認）。
数値に幅があるのは球の位置でガラスの重なり枚数が変わるため。落ちきって受け皿に載っている間は 30fps（`targetFrameRate` の上限）に張り付く。

- M3（avg 30 / min 20）は**コースターはほぼ達成、塔は場面により 20 台前半**。これ以上詰めるなら renderScale を 0.45 以下にする（ぼけと引き換え）か、
  Watch 中だけガラスを間引く。ここまでで対策前の **4.5 倍**。
- 計測の注意: **ランチャーは起動時に `game.json` を読む**ので、解像度を変える試験は *game.json を書き換えてからランチャーを再起動する*。
  また `-screen-width/height` は**フルスクリーンだと無視される**（デスクトップ解像度に丸められる）。窓モード（`-screen-fullscreen 0`）か `renderScale` で変える。
