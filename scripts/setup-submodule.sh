#!/usr/bin/env bash
# NTsLotoEngine: サブモジュールの初期化 + sparse-checkout 設定（NTsWallpaperEngine と同仕様）
# 使い方: リポジトリルートで `bash scripts/setup-submodule.sh`
# sparse 設定は .gitmodules に保存されないため、新規クローン時は必ずこのスクリプトを使う。
set -euo pipefail
cd "$(dirname "$0")/.."

# LotteryBallKit（球。Packages/manifest.json が file:../LotteryBallKit/Assets/LotteryBallKit で参照する）
git submodule update --init --depth 1 LotteryBallKit
echo "OK: LotteryBallKit"

# 100BeautiesLab_CreationsDB（創作DB。将来の球スキン・表示用。読み取り専用）
git submodule update --init --depth 1 100BeautiesLab_CreationsDB
(
  cd 100BeautiesLab_CreationsDB
  git sparse-checkout set --no-cone \
    '/*.md' \
    '/LICENCE' \
    '/data/Dictionaries/**' \
    '/data/Works_NumberTales/DataBases/**' \
    '/data/Works_NumberTales/Dictionaries/**' \
    '/data/Works_NumberTales/RoleplayPrompts/**' \
    '/data/Works_NumberTales/Images/DB_Primary/corefolder/**' \
    '/data/Works_NumberTales/Images/DB_SemiPrimary/corefolder/**' \
    '/data/Works_NumberTales/Images/DB_SelfSecondary/corefolder/**'
)
echo "OK: 100BeautiesLab_CreationsDB (sparse: DataBases + corefolder images + RoleplayPrompts)"

# PenchantManufacture_ImageAssets（フォント。正はサブモジュール。Assets/Fonts の .otf は同期コピー）
git submodule update --init --depth 1 PenchantManufacture_ImageAssets
( cd PenchantManufacture_ImageAssets && git sparse-checkout set --no-cone '/*.md' '/LICENSE' '/assets/fonts/**' )
cp -f PenchantManufacture_ImageAssets/assets/fonts/PenchantManufacture.otf Assets/Fonts/PenchantManufacture.otf
cp -f PenchantManufacture_ImageAssets/LICENSE Assets/Fonts/PenchantManufacture_LICENSE.txt
echo "OK: PenchantManufacture.otf synced to Assets/Fonts (HUD 用)"
