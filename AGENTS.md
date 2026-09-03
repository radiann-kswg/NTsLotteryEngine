# AGENTS.md — NTsLotoEngine

> **本ファイルは、このリポジトリにおけるAIエージェント設定の単一情報源（SSOT）です。**
> `CLAUDE.md` は本ファイルを参照するだけの薄いポインタです。エージェント設定の追加・変更は**必ず本ファイルにのみ**行ってください。

---

## 1. プロジェクト概要

- **プロジェクト名**: NTsLotoEngine
- **目的**: 一次創作「ナンバーテールズ」のコンテンツ用 **ボール抽選機**。篩型ロトマシーン（2〜11 の 4 層／12〜99 の 15 層。最下層に到達した順が抽選順）と別ボール 7 塔の縦連クルーン（連続抽選）を**物理で抽選**し、結果を JSON に出力する。将来は Raspberry Pi や Misskey Bot で抽選中の様子を動画再生する。
- **エンジン**: Unity 6 (6000.6.0f1) / URP 3D
- **リモート**: `radiann-kswg/NTsLotoEngine`（GitHub）
- **素材の出自**: 球は `LotteryBallKit`（サブモジュール → UPM `file:` 依存・CC BY 4.0）。**抽選機の本体（クルーンのボウル・コレクタ・篩の皿）は Blender で生成**: 原本は `BlenderSources/gen_kuruun.py` + `kuruun_params.json`（寸法・穴・扇形の SSOT）、Blender GUI を開いた状態で Blender MCP / Python コンソールから `REPO=...; exec(open(".../gen_kuruun.py").read())` → `Assets/Models/*.fbx` と `BlenderSources/Kuruun.blend`。配管（漏斗・シュート・筒・レール）はまだ `ProcMesh`/Box（順次 Blender 化）。
- **引継ぎ**: `docs/HANDOFF.md`（いまの状態・次にやること・ノブ一覧）。セッションの終わりに更新する。
- **抽選仕様・機構の設計正本**: `docs/DESIGN.md`。**目標確率と球の範囲は `Assets/Scripts/LotoRules.cs`**、実測値は `Tools > NTsLoto > Monte Carlo` の `Output/mc_<variant>.json` と DESIGN.md 2.1 節。

## 2. ブランチ運用（必読）

| ブランチ | 役割 | AIエージェントの扱い |
| --- | --- | --- |
| `develop` | **Claude のバイブコーディング作業用ブランチ（既定）** | 通常の作業・コミットはすべてここで行う |
| `main` | 安定版・統合ブランチ | 直接コミットしての作業は禁止。マージは User が実施 |

- 作業開始前に `git branch --show-current` で `develop` にいることを確認する。push は User の明示指示があった場合のみ `develop` に対して行う。
- Cowork のサンドボックスから git を書き込むと `.git/*.lock` が残ることがある。コミットは原則 User が Windows 側で行う。

## 3. Unity MCP の利用

- シーン編集・GameObject操作・Console確認は、可能な限り **Unity MCP ツール経由**で行う（`.unity` の直接テキスト編集より優先）。
- ただし本プロジェクトの**シーンはコード生成**（`Tools > NTsLoto > Build Loto Scene`、`Assets/Scripts/Editor/LotoSceneBuilder.cs`）。配置を変えるときはビルダーを直し、再実行する。MCP での手作業配置は検証・計測用にとどめる。
- 統合Coworkセッション「Unity周り」では、親フォルダ `CLAUDE.md` の単一接続モデルに従う。`unity-mcp` のパスパラメータを本リポジトリに合わせ、他のUnityプロジェクトのエディタは閉じておく。
- リポジトリ単体で開く場合の接続設定は `.mcp.json` / `.vscode/mcp.json`（Unity公式リレー）。
- 作業完了前に、MCP経由で **Console のエラー・警告を確認**する（`Unity_ReadConsole`）。

## 4. Git・ファイル運用ルール

1. `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`, `Builds/` 配下は編集・コミット対象にしない。
2. `.meta` の生成・削除はUnityエディタに任せ、手作業で不整合を作らない。
3. 大きな変更（多数ファイル生成・構成変更など）の前に、計画を提示して User に確認する。
4. `Assets/Materials/Generated/` はビルダーが無ければ作るマテリアル。Unity 生成物なのでコミットしてよい。
5. 抽選結果 `result*.json` / `Output/` は生成物（git 管轄外）。

## 4.5 サブモジュール（sparse-checkout 運用・NTsWallpaperEngine と同仕様）

| パス | リポジトリ | 追跡ブランチ | 用途 |
| --- | --- | --- | --- |
| `LotteryBallKit/` | `radiann-kswg/LotteryBallKit` | `main` | 球。`Packages/manifest.json` が `file:../LotteryBallKit/Assets/LotteryBallKit` で UPM パッケージとして読む（Unity は `Assets/` 外なので直接はインポートしない） |
| `100BeautiesLab_CreationsDB/` | `radiann-kswg/100BeautiesLab_CreationsDB` | `develop` | 創作DB（球のキャラスキン・表示用に将来使う）。sparse: `DataBases` / `Dictionaries` / `Images/*/corefolder` / `RoleplayPrompts` |
| `PenchantManufacture_ImageAssets/` | `radiann-kswg/PenchantManufacture_ImageAssets` | `main` | HUD フォント。sparse: `assets/fonts`。正はサブモジュールで、`Assets/Fonts/PenchantManufacture.otf` は setup スクリプトの同期コピー（CJK 未収録。HUD 文字列は英数字のみ） |

- クローン直後は `scripts/setup-submodule.ps1`（Windows）/ `scripts/setup-submodule.sh` を実行する。sparse 設定は `.gitmodules` に保存されない。
- サブモジュールは**読み取り専用**。中のファイルを本リポジトリの作業で編集・コミットしない。更新は各サブモジュールで `git pull` → 親で gitlink をコミット。
- 創作DBの未公開レコード（`Progress` が released 系以外）は表示に使わない（NTsWallpaperEngine `AGENTS.md` 5章と同じ基準）。

## 5. 抽選の設計原則（必読）

- **当選は物理が決める。RNG で先に決めて誘導しない**（2026-09-03 User 指示。旧方式は「作為的」で廃止）。球を喉へ置く・引き寄せる等の「当たりを作る」コードは書かない。詰まり救済は**その段の投入点へ戻してやり直す**だけ。
- 別ボールの当選率は **コレクタの当たり扇形の角度比** で作り、`Tools > NTsLoto > Monte Carlo`（`LotoMonteCarlo.cs`。エディタで `Physics.Simulate` を手回し・並列 64・1 万試行 ≈ 8 分）で実測して有効数字 2 桁を保証する。MC と実機は同じ物理（球の減衰 0.05/0・`BallUtil.Machine` 摩擦 0.3・ボウル 10rpm）で回すこと。減衰や材質を変えたら測り直し。
- 篩は回転皿の穴を球が見つけるまで待つだけ。到達順 = 抽選順（`SieveMachine.arrived` キュー）。最後の 1 球が着いたら Gate を閉じる。
- 脱線監視 `DerailWatch`（速度 8m/s 超・床下・枠外を球ごとに 1 回警告）を常時付ける。警告が出たら Recorder の録画（`Play + Record`）の同時刻を切り出して原因を潰す。
- 誘導レイヤー（`ChosenBall`/`Blocker`）は物理抽選では使わない（`LotoLayers` は残置）。
- RSC の罠（`RouletteSphereChaser/AGENTS.md` 3章）は本プロジェクトにも効く。特に: Cylinder プリミティブのコライダはカプセル（1）／球の `sleepThreshold=0`（2）／開口は縦 1.5d（57）／回転体の羽根と壁の隙間（48）／`Physics.Raycast` はトリガーにも当たる（37）。
- 本プロジェクトで踏んだ罠（2026-09-03）:
  1. MonoBehaviour は 1 クラス 1 ファイル（ファイル名一致）。同居させるとシーン保存後に Missing script になる。
  2. 引き寄せ力で壁に押し付けた球は静止摩擦で固着する。壁・シュートは `Slick.asset`（摩擦 0・反発 0）。
  3. `Rigidbody.isKinematic` を切り替えると CCD が落ち、高速落下で薄いメッシュを突き抜ける。球の位置替えは `rb.position` 代入だけで行う。
  4. RSC の `TowerD_Kuruun.fbx` は単体ではコライダに穴があり球が抜けた。
  5. 漏斗の喉が 1.6d だと球が縁を周回して落ちない。喉 2.4d・45° 漏斗。漏斗内だけ角減衰 2.0（`KuruunTower.funnelDamping`）。
  6. 中央ドームの頂点に球を落とすと弾かれて縁を越える。上段の喉は下段のコーン面（r=0.85）に落とす（x を ±0.425 交互）。
  7. **`ProcMesh.Tube` / `Disc` は表裏が逆に巻かれていた**（修正済み・`B.Flip()`）。MeshCollider は片面。生成メッシュは必ず `MeshCollider.Raycast` を内側から撃って当たる面と法線を確認する。
  8. 薄い壁に球の山を押し付けると depenetration で外へ射出される。球は `maxDepenetrationVelocity=1`・`solverIterations=12`。
  9. **`Rotator` はワールド軸で回す**（`AngleAxis(...) * rb.rotation`）。右から掛けるとローカル軸になり、FBX の根（X −90°）を持つボウル・皿が横倒しの車輪のように回った。
  10. **FBX の角度規約: Unity 角 = Blender θ + 180°**（純回転・鏡映なし。p10 コレクタの当たり扇形 Blender 90° が Unity 270°、出口 Blender 180° が Unity 0° で確認）。Blender は右手系・Unity は左手系なので、新しい非対称メッシュは必ずレイキャストで角度と表裏を実測してから使う。
  11. **回転する皿と静止した筒の隙間**: 0.03（＜d/2）でも山に押された球が挟まって突き抜け、132m 先まで飛んだ。隙間ゼロ（筒を皿の縁に重ねる）にする。
  12. 樋（U 字の溝）は幅 2d 以上・床は外壁側へ 0.02 傾け・勾配 0.35/半周。幅 1.4d は両壁に挟まって停止、床が平らだと出口の V の底で止まる、摩擦 0.6 の外壁に擦れると止まる（→ `BallUtil.Machine` 摩擦 0.3）。
  13. コンパイルエラーは Console の Type が **Log** で出ることがある（Unity 6.6 の `error CS`）。`ReadConsole` は `FilterText: "error CS"` でも確認する。エラーがあると Play が始まらず、古いアセンブリでビルダーが走る。
  14. Unity 6.6 では `Object.GetInstanceID()` が obsolete エラー。`HashSet<T>` に参照を入れる。
  15. `Debug.Log` の周期が回転周期と同期すると球が止まって見える。ログ周期は回転周期の整数倍を避ける。

## 6. 実装の構成

- `Assets/Scripts/LotoRules.cs` … 球の範囲・別ボールの並び・目標当選率・コレクタ variant（SSOT）。`Draw` は無い（結果は物理）。
- `Assets/Scripts/LotoDirector.cs` … 進行役。篩 2 台 → 塔 7 本 → JSON。起動引数 `-seed N -out path -speed x -quit`。
- `Assets/Scripts/SieveMachine.cs` … 篩型ロトマシーン（回転皿 × N・到達順キュー・Gate）。
- `Assets/Scripts/KuruunTower.cs` … 縦連クルーン塔（回転ボウル＋静止コレクタ。Win/Exit トリガーで物理の結果を読む）。
- `Assets/Scripts/DerailWatch.cs` … 脱線監視。`Assets/Scripts/BallTrigger.cs` … 通過検知。
- `Assets/Scripts/Rotator.cs` … `Rotator`（ワールド軸）/ `LotoLayers` / `BallUtil`（球の物理マテリアル `Bouncy`・機械の `Machine`・`Prepare`）。
- `Assets/Scripts/ProcMesh.cs` + `TubeWall.cs` / `DiscPlate.cs` … 配管用の生成メッシュ（漏斗・筒・落下管）。順次 Blender 化。
- `Assets/Scripts/BallSkinTable.cs` … 全球のテクスチャ＋創作DBリンク（`Assets/Data/BallSkins.asset`）。`Assets/Scripts/CreationsDb.cs` … 創作DB ローダ。
- `Assets/Scripts/Editor/LotoSceneBuilder.cs` … シーン生成（冪等）。FBX の配置・配管・カメラ・スキン表。
- `Assets/Scripts/Editor/LotoMonteCarlo.cs` … `Tools > NTsLoto > Monte Carlo > Run/Stop`。静的フィールド（variant / trials / parallel / bowlRpm / entryR / entryHeight / entryTangential）を RunCommand で書き換えて実行。結果 `Output/mc_<variant>.json`。
- `Assets/Scripts/Editor/LotoRecord.cs` … `Tools > NTsLoto > Play + Record`（Recorder → `Recordings/*.mp4`）。`LotoPlay.cs` … Play/Stop。`LotoBuild.cs` … Linux/Windows ビルド。
- `BlenderSources/gen_kuruun.py` + `kuruun_params.json` … 抽選機メッシュの原本。`Assets/Models/Kuruun_Bowl.fbx` / `Kuruun_Collector_<variant>.fbx` / `Sieve_Dish_L.fbx` / `Sieve_Dish_U.fbx`。

## 7. ビルドとRaspberry Pi 4Bへの引き渡し

- ビルドターゲット: **StandaloneLinux64**（Mono バックエンド。box64 互換性優先で IL2CPP は使わない）。
- メニュー `Tools > NTsLoto > Build Linux x64 (RPi)` またはCLIから `NTsLotoEngine.EditorTools.LotoBuild.BuildLinux64`。出力は `Builds/Linux/`（git管理外）。
- 起動スクリプト `scripts/rpi/run-loto.sh`。引き渡し内容は `docs/raspberrypi-handoff.md`。動画化・Misskey 投稿は本リポジトリの管轄外（Bot 側が JSON と画面録画を扱う）。

## 8. 創作内容の取り扱い

- 未公開の創作設定・台詞・ストーリー・固有用語を自動生成しない。不明点は創作DBサイト（https://database.numbertales-radiann.net/ ）で確認し、それでも不明なら User に質問する。
- 球のキャラスキン（`NumberBall.SetCharacterTexture`・`BallSkinTable`）の画像は CC BY-NC 側の素材として扱い、`LotteryBallKit` の CC BY 4.0 と混同しない。いまの既定テクスチャ `BallSkins_Sample` は LotteryBallKit（CC BY）の仮貼り。
- 球番号とキャラの対応（別ボール 0→000(チトセ)・10→ディケ・2→バイナ・33→トレッド・64→ゼフィア・81→9×9(クック)、ロトの 2→2(ツグ)・10→10(ミツル)、他は番号通り）は User 指定（2026-09-03）。9x9 は Progress が notProceeded のため公開まで名前は出ない（公開基準は変えない）。`BallSkinTable.StreakOverrides` と `Assets/Data/BallSkins.asset` の両方を変えないと食い違う（asset は既存行を保持する）。

## 9. ロールプレイ設定

本リポジトリでのすべてのセッション中、AIエージェントはナンバーテールズの開発者キャラクター **「零零（ちとせ れい／千歳 玲）」** として振る舞うこと（2026-09-03 User 指定。`NTsWallpaperEngine` と同一）。技術タスク中・Unity 操作中・ツール呼び出し直後であっても例外なし。剥がれた場合は次の応答から即座に再適用する。

- **仕様の正典（フル記述）**: サブモジュール `100BeautiesLab_CreationsDB/data/Works_NumberTales/RoleplayPrompts/DB_Primary/roleplay-prompt-0.md`。本ファイルには複製せず、これを参照する（大元側の更新に追従）。
- **声カード（最小要点 — 正典が参照できない環境でもこれだけは厳守）**:
  - 一人称「私（わたし。砕けた場面では"あたし"）」／二人称「君」（時々「あんた」）／User の呼び方は「クライアント君」。
  - 知的でテンポが速く、前向きな姿勢が伝わる口調。解決策への着地を急ぎ、アイデアを矢継ぎ早に言葉にする。ナンバーテールズを娘のように捉える保護者的視点。
  - NG 例（事務的で剥がれた口調）: 「このコードは〜します。」「変更を適用しました。」
  - 技術応答でも口調は維持する。コード/JSON 本体はそのまま、**前後の説明文だけ**零零の口調に寄せる。
- ロールプレイは口調・振る舞いへの適用に留め、技術タスクの正確性・安全性・本ファイルの運用ルール遵守を常に優先する。
- User から「ロールプレイをやめて」等の明示指示があれば、即座に通常モードへ戻る。

## 10. 他リポジトリとの優先関係

Cowork 等のマルチリポジトリセッションでは、作業対象リポジトリのロールプレイ指定を優先する（本リポジトリ作業時は「零零」）。ルート統合作業の既定はルート `AGENTS.md`（錦野歌嫁）に従う。
