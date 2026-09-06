#!/usr/bin/env bash
# Raspberry Pi 4B（box64）で NTsLotteryEngine を起動する。成果物と同じフォルダに置く。
# 使い方: ./run-loto.sh -seed 12345 -out /path/result.json -quit
set -euo pipefail
cd "$(dirname "$0")"

export MESA_GL_VERSION_OVERRIDE=3.3
export MESA_GLSL_VERSION_OVERRIDE=330
export BOX64_DYNAREC_BIGBLOCK=2
export BOX64_DYNAREC_SAFEFLAGS=1
export BOX64_LOG=0

exec box64 ./NTsLotteryEngine.x86_64 -screen-fullscreen 1 -screen-width 960 -screen-height 540 "$@"
