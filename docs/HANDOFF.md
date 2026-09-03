# HANDOFF.md — 次セッションへの引継ぎ（2026-09-03 時点）

> 読む順: `AGENTS.md`（運用・罠）→ `docs/DESIGN.md`（仕様・機構）→ 本ファイル（いまの状態と次にやること）。

## 1. いまの状態

- **全経路が実機 Play で通っている**: 上段 1 球 → 下段 6 球 → 別ボール塔 7 本 → `result.json`。約 5 分、警告 0（seed 1113669026 / 800858348 で確認）。
- ブランチ `develop`、リモート未設定（`radiann-kswg/NTsLotoEngine` を作って push するのは User）。`main` は Initial commit のまま。
- サブモジュール 3 本は取得済み（sparse）。新規クローン時は `scripts/setup-submodule.ps1`。
- HUD は OnGUI＋PenchantManufacture（`Assets/Fonts/PenchantManufacture.otf`。正はサブモジュール、setup スクリプトが同期コピー）。文字列は英数字のみ（CJK 未収録）。
- Unity MCP: `Tools > NTsLoto > Play / Stop` で MCP からプレイモードを出し入れできる。`LotoDirector.skipDrums` を Inspector（MCP の set_component_property）で true にするとドラムを飛ばして塔だけ試せる（ビルダーは false で生成する）。

## 2. 動かし方（最短）

1. Unity で開く → `Tools > NTsLoto > Build Loto Scene`（冪等）。
2. Play。`result.json` がプロジェクト直下に出る（git 管轄外）。
3. 確率を触ったら `Tools > NTsLoto > Self Check (Monte Carlo)`。
4. ビルド: `Tools > NTsLoto > Build Linux x64 (RPi)`（未実行。初回はモジュールのインストール確認が要る）。

## 3. 未着手・次にやること（優先順）

1. **Linux ビルドの実機確認**（RPi 4B / box64）。`docs/raspberrypi-handoff.md` の手順。ビルド自体もまだ一度も回していない。
2. **ドラムの排出時間短縮**: 下段は 1 球 10〜35 秒（球の輪に阻まれる）。ノブは `LotoDrumTier.drawRpm`(5) / `pullAccel`(6) / 排出口幅（`BuildTier` の 2d）。案: 取り出し時だけフィンを止める、排出口を 2 つにする。
3. **見た目**: 塔は脚 2 本と透明部品だけ。RSC の方針（Blender モデリング主体）に寄せるか、ProcMesh のまま色・照明で整えるかは User 判断。カメラは塔で球追従するが 11 段（約 15 m）は長いので `-speed` か塔ごとのカット割りを検討。
4. **球のキャラスキン**: `NumberBall.SetCharacterTexture`。創作DB サブモジュールの corefolder 画像はそのままでは球面 UV に合わない（LotteryBallKit の `BallUV_Template` に合わせた作画が要る）。NC 素材の区分は `LICENSE.md`。
5. **動画化**: アプリは録画しない。Bot 側で `ffmpeg -f x11grab`（`docs/raspberrypi-handoff.md` 4 章）。
6. **ログの整理**: `KuruunTower` は 5 秒ごとに球の位置を Debug.Log する（調整用）。落ち着いたら消すか `#if UNITY_EDITOR` に。

## 4. 触るときの注意（要点。詳細は AGENTS.md 5 章の「踏んだ罠」）

- 配置を変えるときは `LotoSceneBuilder.cs` を直して `Build Loto Scene`。シーンを手で触らない。
- 球の位置替えは `rb.position` 代入のみ。`isKinematic` を切り替えない（CCD が落ちる）。
- 生成メッシュは片面。表裏は `Cross(b-a, c-a)` が表面法線（`ProcMesh.Bowl` の巻き方向判定）。
- MonoBehaviour は 1 クラス 1 ファイル。
- Cowork サンドボックスの git は lock を消せない。`/tmp/g.sh` 方式（`.git/**/*.lock` を rename してから git を実行）でコミットできる。残骸 `*.lock*.stale` は Windows 側で消す。Desktop Commander 経由の git は自動モードのクラシファイアに拒否される。
- Unity MCP は長時間 Play 中に応答しなくなることがある（Editor が固まったら開き直し）。Camera_Capture は OnGUI を映さない。

## 5. 主要ノブ（Inspector）

| どこ | 何 | 既定 |
| --- | --- | --- |
| `LotoDrumTier` | `stirRpm` / `drawRpm` / `stirSeconds` / `pullAccel` / `timeout` | 40 / 5 / 6 / 6 / 30 |
| `KuruunTower` | `dropVelocity` / `angularDamping` / `linearDamping` / `timeout` / `camAhead` | (−0.57,0,−0.57) / 1.0 / 0.3 / 25 / 0.35 |
| `LotoDirector` | `seed` / `outPath` / `speed` / `quitWhenDone` / `pauseBetween` / `skipDrums` | −1 / result.json / 1 / false / 1.5 / false |
| `LotoSceneBuilder`（定数） | `FunnelH` / `LevelGap` / `Stagger` / `TowerPitch` / `TowerBaseY` | 0.28 / 0.86 / 0.195 / 2.4 / 0.9 |
