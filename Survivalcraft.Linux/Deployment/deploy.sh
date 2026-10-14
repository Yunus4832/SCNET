#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
archive="__SCNET_ARCHIVE__"
image="__SCNET_IMAGE__"
cd "$script_dir"

if [[ ! -f server.conf ]]; then
    cp server.conf.example server.conf
    echo "Created server.conf. Edit it and rerun ./deploy.sh." >&2
    exit 1
fi

# The deployment operator owns this trusted shell configuration file.
source ./server.conf
: "${INSTANCE:=server}" "${SESSION:=survival}" "${WORLD:=World}"
: "${SEED:=123456}" "${GAME_MODE:=Survival}"
: "${SERVER_PORT:=28887}"
: "${HTTP_COMMAND:=false}" "${HTTP_COMMAND_PORT:=28889}" "${HTTP_COMMAND_ACCESS_TOKEN:=}"
: "${CONTENT_SERVER:=}" "${MODS:=}"

: "${P2P:=false}" "${RENDEZVOUS:=}" "${ROOM_NAME:=$WORLD}"
: "${ROOM_DESCRIPTION:=}" "${ROOM_PUBLIC:=false}"
for flag in "$P2P" "$ROOM_PUBLIC" "$HTTP_COMMAND"; do
    case "$flag" in true|false) ;; *) echo "P2P, ROOM_PUBLIC and HTTP_COMMAND must be true or false." >&2; exit 2 ;; esac
done
export SCNET_P2P="$P2P" SCNET_RENDEZVOUS="$RENDEZVOUS" SCNET_ROOM_NAME="$ROOM_NAME"
export SCNET_ROOM_DESCRIPTION="$ROOM_DESCRIPTION" SCNET_ROOM_PUBLIC="$ROOM_PUBLIC"

for name in "$INSTANCE" "$SESSION" "$WORLD"; do
    [[ "$name" =~ ^[A-Za-z0-9_-]+$ ]] || { echo "Invalid instance, session or world name: $name" >&2; exit 2; }
done
[[ "$SEED" =~ ^[A-Za-z0-9_-]+$ ]] || { echo "Invalid seed: $SEED" >&2; exit 2; }
case "$GAME_MODE" in Creative|Harmless|Survival|Challenging|Cruel|Adventure) ;; *) echo "Invalid GAME_MODE" >&2; exit 2 ;; esac
[[ "$SERVER_PORT" =~ ^[0-9]+$ ]] && ((10#$SERVER_PORT >= 1 && 10#$SERVER_PORT <= 65535)) || { echo "Invalid port: $SERVER_PORT" >&2; exit 2; }

[[ "$HTTP_COMMAND_PORT" =~ ^[0-9]+$ ]] && ((10#$HTTP_COMMAND_PORT >= 1 && 10#$HTTP_COMMAND_PORT <= 65535)) || { echo "Invalid HTTP command port: $HTTP_COMMAND_PORT" >&2; exit 2; }
if [[ -n "$HTTP_COMMAND_ACCESS_TOKEN" ]] && { (( ${#HTTP_COMMAND_ACCESS_TOKEN} < 32 )) || [[ "$HTTP_COMMAND_ACCESS_TOKEN" =~ [[:space:]] ]]; }; then
    echo "HTTP_COMMAND_ACCESS_TOKEN must have at least 32 characters and contain no whitespace, or be empty to use the instance token." >&2
    exit 2
fi
SCNET_HTTP_COMMAND_SWITCH=--no-http-command
if [[ "$HTTP_COMMAND" == true ]]; then
    SCNET_HTTP_COMMAND_SWITCH=--http-command
fi
export SCNET_HTTP_COMMAND_SWITCH SCNET_HTTP_COMMAND_PORT="$HTTP_COMMAND_PORT"
export SCNET_HTTP_COMMAND_ACCESS_TOKEN="$HTTP_COMMAND_ACCESS_TOKEN"

if [[ -n "${CONTAINER_ENGINE:-}" ]]; then
    case "$CONTAINER_ENGINE" in podman|docker) engine="$CONTAINER_ENGINE" ;; *) echo "Invalid CONTAINER_ENGINE" >&2; exit 2 ;; esac
elif command -v podman >/dev/null 2>&1; then
    engine=podman
elif command -v docker >/dev/null 2>&1; then
    engine=docker
else
    echo "Podman or Docker is required." >&2
    exit 1
fi
for executable in "$engine" sha256sum python3; do
    command -v "$executable" >/dev/null 2>&1 || { echo "Missing deployment tool: $executable" >&2; exit 1; }
done
if [[ "$engine" == docker ]]; then
    docker compose version >/dev/null 2>&1 || { echo "Docker Compose is required." >&2; exit 1; }
    compose=(docker compose)
elif podman compose version >/dev/null 2>&1; then
    compose=(podman compose)
elif command -v podman-compose >/dev/null 2>&1; then
    compose=(podman-compose)
else
    echo "Podman Compose is required." >&2
    exit 1
fi

SCNET_INSTANCES_DIR="${SCNET_INSTANCES_DIR:-$script_dir/Instances}"
mkdir -p "$SCNET_INSTANCES_DIR/$INSTANCE"
export CONTENT_SERVER MODS SCNET_INSTANCE_PATH="$SCNET_INSTANCES_DIR/$INSTANCE"
export SCNET_SESSION="$SESSION" SCNET_WORLD="$WORLD"

# Resolve all configured versions before changing any persistent configuration.
python3 "$script_dir/configure.py"
sha256sum -c "$archive.sha256"
"$engine" load -i "$archive"
"$engine" image inspect "$image" >/dev/null

export SCNET_INSTANCES_DIR SCNET_INSTANCE="$INSTANCE" SCNET_SESSION="$SESSION" SCNET_WORLD="$WORLD"
export SCNET_SEED="$SEED" SCNET_GAME_MODE="$GAME_MODE" SCNET_SERVER_PORT="$SERVER_PORT"
"${compose[@]}" -f compose.yaml up -d --force-recreate game-server
echo "[SCNET] Game server started: $INSTANCE/$SESSION ($WORLD) on UDP $SERVER_PORT"
if [[ "$HTTP_COMMAND" == true ]]; then
    echo "[SCNET] HTTP control listening on 127.0.0.1:$HTTP_COMMAND_PORT; access remotely through an SSH tunnel."
fi
