# Raspberry Pi 4B 引き渡し資料 — NTsLotteryEngine

「Raspberry Pi OS開発」Coworkプロジェクトへの引き渡し内容。OSイメージへの組み込み（配置・録画・Bot 連携）はあちら側の管轄で、本リポジトリはビルド成果物と起動要件の提供までを担当する。基本構成は NTsWallpaperEngine と同じ（Linux x64 / Mono / box64）。

## 1. 引き渡す成果物

Unityメニュー `Tools > NTsLoto > Build Linux x64 (RPi)` の実行で生成される:

| 内容 | パス |
| --- | --- |
| プレイヤー本体 | `Builds/Linux/NTsLotteryEngine.x86_64` |
| データ一式 | `Builds/Linux/NTsLotteryEngine_Data/` |
| Unityランタイム | `Builds/Linux/UnityPlayer.so` ほか |
| 起動スクリプト | `scripts/rpi/run-loto.sh`（成果物と同じフォルダに配置する） |

- ビルドは **Linux x64 / Mono バックエンド**（box64互換性のため IL2CPP 不使用）。
- ネットワーク・外部データは不要。抽選結果は起動引数 `-out` のパスへ JSON 出力。

## 2. OSイメージ側の要件

NTsWallpaperEngine の引き渡し資料 2 章と同一（Raspberry Pi OS 64-bit デスクトップ版・box64 `-DRPI4ARM64=1`・GPU メモリ 256MB 以上・V3D 有効）。配置想定は `/opt/ntsloto/`。

## 3. 起動

```bash
/opt/ntsloto/run-loto.sh -seed 12345 -out /var/lib/ntsloto/result.json -quit
```

- `-seed N` を省略すると時刻から決める。同じ seed なら同じ結果（映像の再現用）。
- `-quit` で結果出力後 3 秒で終了する（Bot からの一回起動向け）。
- `-speed 2` で 2 倍速（`Time.timeScale`）。
- 3D 物理（球 99 個）のため NTsWallpaperEngine より重い。960x540 で 15fps 前後を想定。重い場合は `Assets/Settings/Mobile_RPAsset.asset` 相当の品質へ落とす（`QualitySettings`）。

## 4. 動画化（Bot 側の参考）

アプリ自体は録画しない。X11 セッション内で画面録画する例:

```bash
ffmpeg -f x11grab -video_size 960x540 -framerate 15 -i :0.0 -c:v libx264 -preset veryfast -pix_fmt yuv420p /tmp/loto.mp4 &
/opt/ntsloto/run-loto.sh -seed "$SEED" -out /tmp/result.json -quit
kill %1
```

抽選時間は 1 回あたり 2〜6 分程度（別ボールの連勝回数で変動）。所要時間を短くしたいときは `-speed`。

## 5. 既知のリスクと代替案

- box64 + Mesa GL オーバーライドは非公式構成（NTsWallpaperEngine と同じ）。3D 物理が乗る分、実機検証は必須。
- 重すぎる場合の代替: PC 側で `-quit` 実行して動画を作り、RPi は再生だけにする（結果 JSON は同じ）。
