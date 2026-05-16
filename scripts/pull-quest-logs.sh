#!/usr/bin/env bash
#
# scripts/pull-quest-logs.sh — pull CampfireVR debug logs off a connected Quest.
#
# Usage:
#   ./scripts/pull-quest-logs.sh           # pull → quest-logs/YYYYMMDD-HHMMSS/
#   ./scripts/pull-quest-logs.sh --zip     # ...also produce quest-logs/campfirevr-logs-YYYYMMDD-HHMMSS.zip
#   ./scripts/pull-quest-logs.sh --help
#
# Requirements:
#   * adb (one of):
#       - Android Platform Tools on $PATH
#       - Unity Hub Android module at $UNITY_VERSION (default 6000.4.7f1)
#       - $ADB env var pointing at an adb binary
#   * Exactly one Quest connected via USB-C with Developer Mode enabled and
#     the "Allow USB debugging" popup accepted inside the headset.
#   * CampfireVR launched at least once on that Quest (so the debug-logs
#     directory exists in its app-private storage).
#
# What it produces:
#   quest-logs/YYYYMMDD-HHMMSS/
#     campfirevr-log-*.jsonl       (whatever was on the device)
#     README.txt                   (pull timestamp, device id, app version,
#                                   source path, command used)
#
# Exit codes:
#   0 — success
#   1 — bad CLI args
#   2 — adb not found
#   3 — device count != 1 (zero connected or more than one)
#   4 — device unauthorized (Allow USB debugging popup not accepted)
#   5 — device in unexpected state (offline / no permissions / etc.)
#   6 — no debug-logs directory on device (app never launched, or wrong package)

set -euo pipefail

# Project-bound. Override with PACKAGE env var if the app is ever renamed.
PACKAGE="${PACKAGE:-com.unitymcplab.campfireroom}"
LOG_DIR_ON_DEVICE="/sdcard/Android/data/${PACKAGE}/files/debug-logs"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUTPUT_BASE="${REPO_ROOT}/quest-logs"
STAMP="$(date +%Y%m%d-%H%M%S)"
ZIP_OUTPUT=0

usage() {
    cat <<EOF
Usage: $(basename "$0") [--zip] [--help]

Pulls CampfireVR debug logs from the single Quest currently connected via USB.
Saves them to ${OUTPUT_BASE#"${REPO_ROOT}/"}/<timestamp>/ alongside a small
README.txt that records the pull metadata.

Options:
  --zip       After the pull, produce
              ${OUTPUT_BASE#"${REPO_ROOT}/"}/campfirevr-logs-<timestamp>.zip
  -h, --help  Show this message.

Env:
  ADB              Absolute path to an adb binary (overrides discovery).
  UNITY_VERSION    Unity Editor version (default: 6000.4.7f1) — used to
                   locate the bundled adb if no PATH adb is available.
  PACKAGE          Android package name (default: ${PACKAGE}).
EOF
}

# --- argument parsing ----------------------------------------------------

for arg in "$@"; do
    case "$arg" in
        --zip)     ZIP_OUTPUT=1 ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown arg: $arg" >&2; usage >&2; exit 1 ;;
    esac
done

# --- locate adb ----------------------------------------------------------

UNITY_VERSION="${UNITY_VERSION:-6000.4.7f1}"
UNITY_ADB="/Applications/Unity/Hub/Editor/${UNITY_VERSION}/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb"

if [[ -n "${ADB:-}" ]]; then
    if [[ ! -x "$ADB" ]]; then
        echo "ADB env var set to '$ADB' but that file isn't executable." >&2
        exit 2
    fi
elif command -v adb >/dev/null 2>&1; then
    ADB="$(command -v adb)"
elif [[ -x "$UNITY_ADB" ]]; then
    ADB="$UNITY_ADB"
else
    cat >&2 <<EOF
adb not found.

Install Android Platform Tools:
  https://developer.android.com/studio/releases/platform-tools

Unzip and either add the folder to your PATH, or set:
  export ADB=/absolute/path/to/adb

(If you have Unity Hub with Android module installed at Unity ${UNITY_VERSION},
this script would also find adb at:
  ${UNITY_ADB})
EOF
    exit 2
fi

# --- verify exactly one authorized device --------------------------------

# `adb devices` output: header line, then one row per device:
#   2G0YC5ZG20031Y    device
# Possible second-column states: device, unauthorized, offline, no permissions.
#
# (`mapfile` would be one line but it's bash 4+; macOS ships bash 3.2.)
DEVICE_LINES=()
while IFS= read -r _line; do
    DEVICE_LINES+=("$_line")
done < <("$ADB" devices | tail -n +2 | awk 'NF > 0')

case "${#DEVICE_LINES[@]}" in
    0)
        cat >&2 <<EOF
No Quest connected. Plug the headset in via USB-C, then:
  1. Inside the headset, accept the "Allow USB debugging" popup.
  2. Verify with: ${ADB} devices
EOF
        exit 3
        ;;
    1) ;;
    *)
        echo "Multiple devices connected (${#DEVICE_LINES[@]}). Disconnect all but the one Quest you want to pull from:" >&2
        printf '  %s\n' "${DEVICE_LINES[@]}" >&2
        exit 3
        ;;
esac

DEVICE_ID="$(awk '{print $1}' <<<"${DEVICE_LINES[0]}")"
DEVICE_STATE="$(awk '{print $2}' <<<"${DEVICE_LINES[0]}")"

case "$DEVICE_STATE" in
    device) ;;
    unauthorized)
        cat >&2 <<EOF
Device ${DEVICE_ID} is unauthorized.

Put the headset on and accept the "Allow USB debugging from this computer"
popup. If you missed it, unplug + replug the USB cable to bring it back up.
EOF
        exit 4
        ;;
    *)
        echo "Device ${DEVICE_ID} is in unexpected state: '${DEVICE_STATE}'." >&2
        echo "Try unplug + replug, or restart the headset." >&2
        exit 5
        ;;
esac

# --- verify the app's log directory exists on the device -----------------

if ! "$ADB" -s "$DEVICE_ID" shell "ls $LOG_DIR_ON_DEVICE" >/dev/null 2>&1; then
    cat >&2 <<EOF
No debug-logs directory found at:
  $LOG_DIR_ON_DEVICE

Has CampfireVR (package ${PACKAGE}) been launched at least once on this
Quest? The DebugLogger creates the directory on first launch.
EOF
    exit 6
fi

# --- read app version (best-effort) --------------------------------------

APP_VERSION="$( \
    "$ADB" -s "$DEVICE_ID" shell "dumpsys package ${PACKAGE} 2>/dev/null" \
    | grep -m1 versionName \
    | tr -d ' \r' \
    | cut -d'=' -f2 \
    || echo "")"
APP_VERSION="${APP_VERSION:-unknown}"

# --- pull -----------------------------------------------------------------

OUT_DIR="${OUTPUT_BASE}/${STAMP}"
mkdir -p "$OUT_DIR"

echo "[pull] Device: ${DEVICE_ID}  ·  App version: ${APP_VERSION}"
echo "[pull] Source: ${LOG_DIR_ON_DEVICE}"
echo "[pull] Output: ${OUT_DIR#"${REPO_ROOT}/"}/"
"$ADB" -s "$DEVICE_ID" pull "${LOG_DIR_ON_DEVICE}/." "${OUT_DIR}/" 2>&1 \
    | sed 's/^/[pull] /'

LOG_COUNT="$(find "$OUT_DIR" -maxdepth 1 -name '*.jsonl' 2>/dev/null | wc -l | tr -d ' ')"

# --- write README.txt -----------------------------------------------------

cat > "${OUT_DIR}/README.txt" <<EOF
CampfireVR debug-log pull
=========================

Pulled at:              $(date '+%Y-%m-%d %H:%M:%S %z')
Device serial:          ${DEVICE_ID}
App package:            ${PACKAGE}
App version:            ${APP_VERSION}    (Application.version / Android bundleVersion)
Source path on device:  ${LOG_DIR_ON_DEVICE}
File count pulled:      ${LOG_COUNT}
Command used:           $(basename "$0") $*

See docs/debug-logging.md for the JSONL schema and triage recipes.
See docs/release-process.md for how the version number maps to a build tag.
EOF

# --- optional zip --------------------------------------------------------

if [[ "$ZIP_OUTPUT" -eq 1 ]]; then
    if ! command -v zip >/dev/null 2>&1; then
        echo "[pull] zip not on PATH — skipping --zip step." >&2
    else
        ZIP_NAME="campfirevr-logs-${STAMP}.zip"
        ( cd "$OUTPUT_BASE" && zip -qr "$ZIP_NAME" "$STAMP/" )
        echo "[pull] Zipped:  ${OUTPUT_BASE#"${REPO_ROOT}/"}/${ZIP_NAME}"
    fi
fi

echo "[pull] OK · ${LOG_COUNT} log file(s) at ${OUT_DIR#"${REPO_ROOT}/"}/"
