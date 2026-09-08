#!/usr/bin/env bash
# 此文件是部署脚本模板，由 build-image.sh 写入当前镜像和归档信息。
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ARCHIVE_NAME="__CONTENTSERVER_ARCHIVE__"
IMAGE_REFERENCE="__CONTENTSERVER_IMAGE__"

cd "$SCRIPT_DIR"

if [ -n "${CONTAINER_ENGINE:-}" ]; then
  case "$CONTAINER_ENGINE" in
    docker|podman)
      container_engine="$CONTAINER_ENGINE"
      ;;
    *)
      echo "[ContentServer] CONTAINER_ENGINE 仅支持 docker 或 podman: $CONTAINER_ENGINE" >&2
      exit 1
      ;;
  esac
elif command -v podman >/dev/null 2>&1; then
  container_engine="podman"
elif command -v docker >/dev/null 2>&1; then
  container_engine="docker"
else
  echo "[ContentServer] 缺少容器引擎: 请安装 Podman 或 Docker。" >&2
  exit 1
fi

for command_name in sha256sum "$container_engine"; do
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "[ContentServer] 缺少部署工具: $command_name" >&2
    exit 1
  fi
done

confirm() {
  local prompt="$1"
  local answer

  read -r -p "$prompt [Y/n] " answer || return 1
  case "$answer" in
    ""|y|Y|yes|YES|Yes)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

echo "[ContentServer] Container engine: $container_engine"
echo "[ContentServer] Image: $IMAGE_REFERENCE"

if confirm "是否校验并导入镜像？"; then
  previous_image_id="$("$container_engine" image inspect --format '{{.Id}}' "$IMAGE_REFERENCE" 2>/dev/null || true)"
  sha256sum -c "$ARCHIVE_NAME.sha256"
  "$container_engine" load -i "$ARCHIVE_NAME"
  current_image_id="$("$container_engine" image inspect --format '{{.Id}}' "$IMAGE_REFERENCE")"
  if [ -n "$previous_image_id" ] && [ "$previous_image_id" != "$current_image_id" ] &&
     [ "$("$container_engine" image inspect --format '{{len .RepoTags}}' "$previous_image_id")" = 0 ]; then
    if "$container_engine" image rm "$previous_image_id" >/dev/null; then
      echo "[ContentServer] Removed superseded image: $previous_image_id"
    else
      echo "[ContentServer] 警告: 旧镜像仍被容器引用，未能清理: $previous_image_id" >&2
    fi
  fi
else
  echo "[ContentServer] 已跳过镜像导入。"
fi

if ! confirm "是否启动 ContentServer？"; then
  echo "[ContentServer] 已跳过容器启动。"
  exit 0
fi

if ! "$container_engine" image inspect "$IMAGE_REFERENCE" >/dev/null 2>&1; then
  echo "[ContentServer] 本地不存在镜像: $IMAGE_REFERENCE" >&2
  exit 1
fi

if [ "$container_engine" = docker ]; then
  if ! docker compose version >/dev/null 2>&1; then
    echo "[ContentServer] Docker Compose 不可用。" >&2
    exit 1
  fi
  compose_command=(docker compose)
elif podman compose version >/dev/null 2>&1; then
  compose_command=(podman compose)
elif command -v podman-compose >/dev/null 2>&1; then
  compose_command=(podman-compose)
else
  echo "[ContentServer] Podman Compose 不可用。" >&2
  exit 1
fi

mkdir -p Data
"${compose_command[@]}" -f compose.yaml up -d
echo "[ContentServer] ContentServer 已启动。"
