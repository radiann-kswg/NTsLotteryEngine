# NTsLotoEngine

ナンバーテールズのコンテンツ用ボール抽選機（Unity 6 / URP）。
実物の宝くじ抽選機（電動攪拌式遠心力型・夢ロトくん）を参考にした**二層式ロトマシーン**と、**別ボール 7 塔の縦連クルーン**で抽選の様子を演出し、結果を JSON に出力します。

| 抽選 | 内容 |
| --- | --- |
| 上段 | 1〜11 から 1 個 |
| 下段 | 12〜99 から 6 個 |
| 別ボール 1 / 10 | それぞれ 1/11・6/88 の単発抽選 |
| 別ボール 0 / 2 | 10%×最大3連・50%×最大10連 |
| 別ボール 33 / 64 / 81 | 完走確率が P=1/1024 になる連続抽選（最大 11 / 6 / 4 連） |

詳細は [docs/DESIGN.md](docs/DESIGN.md)。確率の正本は `Assets/Scripts/LotoRules.cs`。

## 動作環境

- Unity **6000.6.0f1**（URP 17.6）
- 球: [LotteryBallKit](https://github.com/radiann-kswg/LotteryBallKit)（サブモジュール `LotteryBallKit/` を `Packages/manifest.json` が `file:` 参照）
- クルーン: [RouletteSphereChaser](https://github.com/radiann-kswg/RouletteSphereChaser) の `TowerD_Kuruun.fbx`（同梱）
- 対応プラットフォーム: Windows x64 / **Linux x64**（Raspberry Pi 4B は box64 経由。`docs/raspberrypi-handoff.md`）

## 動かしかた

1. クローン後に `scripts/setup-submodule.ps1`（または `.sh`）でサブモジュールを取得してから Unity で開く。
2. `Tools > NTsLoto > Build Loto Scene` でシーンを生成（冪等。`Assets/Scenes/LotoScene.unity` に保存）。
3. Play。上段 → 下段 → 別ボール 7 台の順に自動で進み、最後に `result.json` を書く。
4. `Tools > NTsLoto > Self Check (Monte Carlo)` で確率の自己検査。

ビルド: `Tools > NTsLoto > Build Linux x64 (RPi)` / `Build Windows x64`。
起動引数: `-seed N` / `-out path` / `-speed x` / `-quit`。

## 構成

```
Assets/Scripts/            LotoRules / LotoDirector / LotoDrumTier / KuruunTower / ProcMesh / TubeWall / DiscPlate / BallTrigger / Rotator
Assets/Scripts/Editor/     LotoSceneBuilder（シーン生成）/ LotoBuild / LotoSelfCheck / LotoPlay
Assets/RouletteSphereChaser/Models/  TowerD_Kuruun.fbx（CC BY 4.0）
Assets/Settings/           URP 設定（PC / Mobile）
docs/                      DESIGN.md / raspberrypi-handoff.md
scripts/                   setup-submodule.ps1 / .sh、rpi/run-loto.sh
LotteryBallKit/ 100BeautiesLab_CreationsDB/ PenchantManufacture_ImageAssets/  サブモジュール（AGENTS.md 4.5）
```

## ライセンス

リポジトリ既定は **CC BY-NC 4.0**（百花繚乱研究所 / ラジアン）。LotteryBallKit・RouletteSphereChaser 由来の素材は CC BY 4.0。詳細は [LICENSE.md](LICENSE.md)。
