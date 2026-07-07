#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"

export MOD_SERVER_IMAGE="${MOD_SERVER_IMAGE:-registry.cn-hangzhou.aliyuncs.com/yunus4832/mod_server}"
export MOD_SERVER_TAG="${MOD_SERVER_TAG:-$(sed -n 's:.*<ScNetworkVersion>\(.*\)</ScNetworkVersion>.*:\1:p' "$ROOT_DIR/Directory.Build.props" | head -n 1)}"

if [[ -z "$MOD_SERVER_TAG" ]]; then
  echo "Unable to determine image tag. Set MOD_SERVER_TAG explicitly." >&2
  exit 1
fi

if [[ -n "${COMPOSE_TOOL:-}" ]]; then
  read -r -a COMPOSE_CMD <<< "$COMPOSE_TOOL"
elif command -v podman >/dev/null 2>&1; then
  COMPOSE_CMD=(podman compose)
elif command -v docker >/dev/null 2>&1; then
  COMPOSE_CMD=(docker compose)
else
  echo "Neither podman nor docker was found. Set COMPOSE_TOOL, for example: COMPOSE_TOOL='docker compose'." >&2
  exit 1
fi

"${COMPOSE_CMD[@]}" -f "$SCRIPT_DIR/compose.yaml" "$@"
