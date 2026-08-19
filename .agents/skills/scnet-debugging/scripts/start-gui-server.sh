#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
instance_name=""
session_name=""
world_name=""
player_name=""
server_port=""
broadcast_port=""
game_mode=""
skip_build=false
keep_instance=false
instance_dir=""
instance_created=false

usage() {
    echo "Usage: $0 --instance NAME --session NAME --world NAME --player NAME [--game-mode MODE] [--server-port PORT] [--broadcast-port PORT] [--no-build] [--keep-instance]"
}

while (($# > 0)); do
    case "$1" in
        --instance) instance_name="${2:-}"; shift 2 ;;
        --session) session_name="${2:-}"; shift 2 ;;
        --world) world_name="${2:-}"; shift 2 ;;
        --player) player_name="${2:-}"; shift 2 ;;
        --game-mode) game_mode="${2:-}"; shift 2 ;;
        --server-port) server_port="${2:-}"; shift 2 ;;
        --broadcast-port) broadcast_port="${2:-}"; shift 2 ;;
        --no-build) skip_build=true; shift ;;
        --keep-instance) keep_instance=true; shift ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown argument: $1" >&2; usage >&2; exit 2 ;;
    esac
done

if [[ -z "$instance_name" || -z "$session_name" || -z "$world_name" || -z "$player_name" ]]; then
    usage >&2
    exit 2
fi

if ! [[ "$instance_name" =~ ^[A-Za-z0-9_-]+$ ]]; then
    echo "--instance must contain only ASCII letters, digits, '-' and '_'." >&2
    exit 2
fi

for port_value in "$server_port" "$broadcast_port"; do
    if [[ -n "$port_value" ]] && (! [[ "$port_value" =~ ^[0-9]+$ ]] || ((port_value < 1 || port_value > 65535))); then
        echo "Port values must be integers from 1 to 65535." >&2
        exit 2
    fi
done
if [[ -n "$game_mode" ]]; then
    case "${game_mode,,}" in
        creative|harmless|survival|challenging|cruel|adventure) ;;
        *) echo "--game-mode must be Creative, Harmless, Survival, Challenging, Cruel, or Adventure." >&2; exit 2 ;;
    esac
fi

if [[ "$skip_build" == false ]]; then
    dotnet build "$repo_root/Survivalcraft.Linux/Survivalcraft.Linux.csproj" --no-restore
fi

starter="$repo_root/Survivalcraft.Linux/bin/Debug/net10.0/linux-x64/SurvivalcraftStarter"
instance_dir="$(dirname "$starter")/Instances/$instance_name"
if [[ ! -d "$instance_dir" ]]; then
    instance_created=true
fi
args=(
    "$starter"
    --instance "$instance_name"
    --gui
    --host
    --session "$session_name"
    --world "$world_name"
    --player "$player_name"
    --log-level Debug
)

if [[ -n "$server_port" ]]; then
    args+=(--server-port "$server_port")
fi
if [[ -n "$broadcast_port" ]]; then
    args+=(--broadcast-port "$broadcast_port")
fi
if [[ -n "$game_mode" ]]; then
    args+=(--game-mode "$game_mode")
fi

printf 'Starting GUI server:'
printf ' %q' "${args[@]}"
printf '\n'
set +e
"${args[@]}"
exit_code=$?
set -e

if [[ "$instance_created" == true && -d "$instance_dir" ]]; then
    if [[ "$exit_code" -eq 0 && "$keep_instance" != true &&
          "$(dirname "$instance_dir")" == "$(dirname "$starter")/Instances" &&
          "$(basename "$instance_dir")" == "$instance_name" ]]; then
        rm -rf -- "$instance_dir"
        echo "Deleted temporary instance: $instance_dir"
    else
        echo "Preserved debug instance: $instance_dir"
    fi
fi

exit "$exit_code"
