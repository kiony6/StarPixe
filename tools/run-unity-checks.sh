#!/usr/bin/env bash
#
# 本地一键运行 Unity 侧校验：脚本编译校验 -> 进程内确定性仿真 -> 真实 UDP 多进程联机测试。
# 需本机安装 Unity 2022.3.55f1（或兼容版本）。通过环境变量 UNITY_BIN 指定编辑器可执行文件。
#
#   UNITY_BIN="/Applications/Unity/Hub/Editor/2022.3.55f1/Unity.app/Contents/MacOS/Unity" \
#     tools/run-unity-checks.sh
#
# Linux 示例：
#   UNITY_BIN="/opt/unity/2022.3.55f1/Editor/Unity" tools/run-unity-checks.sh
#
# 只跑其中一项：
#   tools/run-unity-checks.sh compile
#   tools/run-unity-checks.sh simulation
#   tools/run-unity-checks.sh udp
#
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_BIN="${UNITY_BIN:-unity}"
LOG_DIR="${PROJECT_PATH}/Logs"
mkdir -p "${LOG_DIR}"

run_unity() {
    local name="$1"
    shift
    echo "==> ${name}"
    "${UNITY_BIN}" \
        -batchmode \
        -quit \
        -nographics \
        -projectPath "${PROJECT_PATH}" \
        -logFile "${LOG_DIR}/${name}.log" \
        "$@"
    echo "    ok (log: Logs/${name}.log)"
}

run_compile() {
    run_unity compile-verify \
        -executeMethod StarPixelSurge.EditorTools.ScriptCompileVerifier.VerifyFromCommandLine
}

run_simulation() {
    run_unity simulation \
        -executeMethod StarPixelSurge.EditorTools.NetworkSimulationRunner.RunFromCommandLine
}

run_udp() {
    run_unity udp-playtest \
        -executeMethod StarPixelSurge.EditorTools.UdpTwoProcessTestRunner.RunFromCommandLine \
        -playtestClients 2 \
        -playtestDuration 12 \
        -playtestLatency 0.08 \
        -playtestJitter 0.03 \
        -playtestLossRate 0.05
}

target="${1:-all}"
case "${target}" in
    compile) run_compile ;;
    simulation) run_simulation ;;
    udp) run_udp ;;
    all)
        run_compile
        run_simulation
        run_udp
        ;;
    *)
        echo "unknown target: ${target}" >&2
        echo "usage: $0 [all|compile|simulation|udp]" >&2
        exit 2
        ;;
esac

echo "all requested checks passed"
