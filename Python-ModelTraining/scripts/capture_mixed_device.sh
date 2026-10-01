#!/bin/bash
# Run from any directory. Existing run directories are never overwritten.
set -u
script_dir="$(cd -- "$(dirname -- "$0")" && pwd)"
project_root="$(cd -- "$script_dir/../.." && pwd)"
run_id="${1:-mixed-device-6000-640}"
frame_count="${2:-6000}"
if [[ ! "$run_id" =~ ^[a-zA-Z0-9][a-zA-Z0-9._-]*$ || ! "$frame_count" =~ ^[1-9][0-9]*$ ]]; then
    echo 'Usage: bash capture_mixed_device.sh [unique-run-id] [positive-frame-count]'
    exit 2
fi
output_root="$project_root/Python-ModelTraining/data/generated"
run_dir="$output_root/$run_id"
if [[ -e "$run_dir" ]]; then
    echo "Run already exists: $run_dir. Choose a new run ID."
    exit 2
fi
unity_bin="${UNITY_CAPTURE_BIN:-/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity}"
log_path="$(mktemp "/tmp/${run_id}.XXXXXX")"
echo "Capture: $run_id ($frame_count frames)"
echo "Output: $run_dir"
echo "Unity log: $log_path"
"$unity_bin" -batchmode -projectPath "$project_root/Unity-SyntheticDataGenrator" \
    -executeMethod SyntheticData.Editor.CaptureRunCommand.Run \
    -captureRunId "$run_id" -captureFrameCount "$frame_count" -captureSeed 42 \
    -captureOutputRoot "$output_root" -captureMixedDeviceProfiles -logFile "$log_path" &
capture_pid=$!
trap 'kill -TERM "$capture_pid" 2>/dev/null; wait "$capture_pid"; exit 130' INT TERM
while kill -0 "$capture_pid" 2>/dev/null; do
    captured=0
    if [[ -d "$run_dir/images" ]]; then
        captured=$(find "$run_dir/images" -type f -name 'frame_*.png' | wc -l | tr -d ' ')
    fi
    printf '\rGenerated: %s / %s (Unity PID %s)       ' "$captured" "$frame_count" "$capture_pid"
    sleep 3
done
wait "$capture_pid"
capture_exit=$?
printf '\nUnity finished with exit status %s\n' "$capture_exit"
if [[ "$capture_exit" != 0 ]]; then tail -60 "$log_path"; fi
echo "Log retained: $log_path"
exit "$capture_exit"
