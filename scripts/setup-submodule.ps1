# NTsLotoEngine: サブモジュールの初期化 + sparse-checkout 設定（NTsWallpaperEngine と同仕様）
# 使い方: リポジトリルートで `pwsh -File scripts/setup-submodule.ps1`
# sparse 設定は .gitmodules に保存されないため、新規クローン時は必ずこのスクリプトを使う。
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

# LotteryBallKit（球。Packages/manifest.json が file:../LotteryBallKit/Assets/LotteryBallKit で参照する）
git submodule update --init --depth 1 LotteryBallKit
Write-Host "OK: LotteryBallKit"

# 100BeautiesLab_CreationsDB（創作DB。将来の球スキン・表示用。読み取り専用）
git submodule update --init --depth 1 100BeautiesLab_CreationsDB
Set-Location 100BeautiesLab_CreationsDB
git sparse-checkout set --no-cone `
  '/*.md' `
  '/LICENCE' `
  '/data/Dictionaries/**' `
  '/data/Works_NumberTales/DataBases/**' `
  '/data/Works_NumberTales/Dictionaries/**' `
  '/data/Works_NumberTales/RoleplayPrompts/**' `
  '/data/Works_NumberTales/Images/DB_Primary/corefolder/**' `
  '/data/Works_NumberTales/Images/DB_SemiPrimary/corefolder/**' `
  '/data/Works_NumberTales/Images/DB_SelfSecondary/corefolder/**'
Set-Location ..
Write-Host "OK: 100BeautiesLab_CreationsDB (sparse: DataBases + corefolder images + RoleplayPrompts)"

# PenchantManufacture_ImageAssets（フォント。HUD で使うようになったら Assets/Fonts へ同期する）
git submodule update --init --depth 1 PenchantManufacture_ImageAssets
Set-Location PenchantManufacture_ImageAssets
git sparse-checkout set --no-cone '/*.md' '/LICENSE' '/assets/fonts/**'
Set-Location ..
Write-Host "OK: PenchantManufacture_ImageAssets (sparse: assets/fonts)"
