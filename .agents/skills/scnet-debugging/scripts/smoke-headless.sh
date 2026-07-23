#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
session_name=""
world_name=""
seed=""
startup_timeout=60
artifact_dir=""
skip_build=false
server_pid=""
input_fifo=""

usage() {
    echo "Usage: $0 --session NAME --world NAME [--seed VALUE] [--timeout SECONDS] [--artifacts DIR] [--no-build]"
}

while (($# > 0)); do
    case "$1" in
        --session)
            session_name="${2:-}"
            shift 2
            ;;
        --world)
            world_name="${2:-}"
            shift 2
            ;;
        --seed)
            seed="${2:-}"
            shift 2
            ;;
        --timeout)
            startup_timeout="${2:-}"
            shift 2
            ;;
        --artifacts)
            artifact_dir="${2:-}"
            shift 2
            ;;
        --no-build)
            skip_build=true
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "Unknown argument: $1" >&2
            usage >&2
            exit 2
            ;;
    esac
done

if [[ -z "$session_name" || -z "$world_name" ]]; then
    usage >&2
    exit 2
fi

if ! [[ "$startup_timeout" =~ ^[1-9][0-9]*$ ]]; then
    echo "--timeout must be a positive integer." >&2
    exit 2
fi

if [[ -z "$artifact_dir" ]]; then
    artifact_dir="$(mktemp -d -t scnet-smoke-XXXXXXXX)"
else
    mkdir -p "$artifact_dir"
    artifact_dir="$(cd "$artifact_dir" && pwd)"
fi

server_log="$artifact_dir/server.log"
metadata_file="$artifact_dir/run.txt"
input_fifo="$artifact_dir/server.stdin"

cleanup() {
    exec 3>&- 2>/dev/null || true
    if [[ -n "$server_pid" ]] && kill -0 "$server_pid" 2>/dev/null; then
        kill -INT "$server_pid" 2>/dev/null || true
        for _ in {1..50}; do
            if ! kill -0 "$server_pid" 2>/dev/null; then
                break
            fi
            sleep 0.2
        done
        if kill -0 "$server_pid" 2>/dev/null; then
            kill -TERM "$server_pid" 2>/dev/null || true
        fi
    fi
    [[ -n "$input_fifo" ]] && rm -f "$input_fifo"
}
trap cleanup EXIT INT TERM

cd "$repo_root"

if [[ "$skip_build" != true ]]; then
    dotnet build Survivalcraft.Linux/Survivalcraft.Linux.csproj
fi

starter="$repo_root/Survivalcraft.Linux/bin/Debug/net10.0/linux-x64/SurvivalcraftStarter"
if [[ ! -x "$starter" ]]; then
    echo "Linux starter was not produced at $starter." >&2
    exit 1
fi

args=(
    "$starter"
    --server
    --session "$session_name"
    --world "$world_name"
    --log-level Debug
)
if [[ -n "$seed" ]]; then
    args+=(--seed "$seed")
fi

{
    echo "started_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
    printf "command="
    printf "%q " "${args[@]}"
    echo
    echo "session=$session_name"
    echo "world=$world_name"
} > "$metadata_file"

mkfifo "$input_fifo"
"${args[@]}" < "$input_fifo" > "$server_log" 2>&1 &
server_pid=$!
exec 3> "$input_fifo"

ready=false
for ((attempt = 0; attempt < startup_timeout * 5; attempt++)); do
    if grep -q "Headless server started" "$server_log"; then
        ready=true
        break
    fi
    if ! kill -0 "$server_pid" 2>/dev/null; then
        break
    fi
    sleep 0.2
done

if [[ "$ready" != true ]]; then
    echo "Headless server did not become ready. Artifacts: $artifact_dir" >&2
    tail -n 80 "$server_log" >&2 || true
    exit 1
fi

printf "help\n" >&3
printf "permission\n" >&3
printf "time get\n" >&3

commands_ready=false
for _ in {1..50}; do
    if [[ "$(grep -c "COMMAND OK" "$server_log" || true)" -ge 3 ]]; then
        commands_ready=true
        break
    fi
    if ! kill -0 "$server_pid" 2>/dev/null; then
        break
    fi
    sleep 0.2
done

if [[ "$commands_ready" != true ]]; then
    echo "Server did not complete diagnostic commands. Artifacts: $artifact_dir" >&2
    tail -n 80 "$server_log" >&2 || true
    exit 1
fi

if rg -n "ERROR:|Unhandled exception|COMMAND ERROR" "$server_log"; then
    echo "Smoke test observed an error. Artifacts: $artifact_dir" >&2
    exit 1
fi

printf "stop\n" >&3
for _ in {1..50}; do
    if ! kill -0 "$server_pid" 2>/dev/null; then
        break
    fi
    sleep 0.2
done
if kill -0 "$server_pid" 2>/dev/null; then
    echo "Headless server did not stop gracefully. Artifacts: $artifact_dir" >&2
    exit 1
fi

set +e
wait "$server_pid"
exit_code=$?
set -e
server_pid=""
echo "exit_code=$exit_code" >> "$metadata_file"
echo "finished_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)" >> "$metadata_file"

if [[ "$exit_code" -ne 0 ]]; then
    echo "Headless server exited with code $exit_code. Artifacts: $artifact_dir" >&2
    exit "$exit_code"
fi

echo "SCNET Headless smoke test passed. Artifacts: $artifact_dir"
