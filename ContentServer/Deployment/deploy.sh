#!/usr/bin/env bash
# 此文件是部署脚本模板，由 build-image.sh 写入当前镜像和归档信息。
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ARCHIVE_NAME="__CONTENTSERVER_ARCHIVE__"
IMAGE_REFERENCE="__CONTENTSERVER_IMAGE__"
import_choice="ask"
start_choice="ask"
deployment_mode=""
use_defaults=false

usage() {
  cat <<'EOF'
用法: ./deploy.sh [选项]

  --import              校验并导入镜像
  --skip-import         跳过镜像导入
  --http                使用 HTTP 部署
  --https               使用 Caddy HTTPS 部署
  --no-start            不启动容器
  --default             跳过所有问题，使用默认选项导入镜像并通过 HTTP 启动
  -h, --help            显示帮助

无人值守示例: ./deploy.sh --default
EOF
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --import)
      import_choice="yes"
      ;;
    --skip-import)
      import_choice="no"
      ;;
    --http)
      deployment_mode="http"
      ;;
    --https)
      deployment_mode="https"
      ;;
    --no-start)
      start_choice="no"
      ;;
    --default)
      use_defaults=true
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "[ContentServer] 未知参数: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
  shift
done

if [ "$use_defaults" = true ]; then
  [ "$import_choice" != ask ] || import_choice="yes"
  [ "$start_choice" != ask ] || start_choice="yes"
  [ -n "$deployment_mode" ] || deployment_mode="http"
fi

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
  local answer=""

  if ! read -r -p "$prompt [Y/n] " answer; then
    echo "[ContentServer] 无法读取交互输入；无人值守部署请传入 --default 或完整参数。" >&2
    exit 1
  fi
  case "$answer" in
    ""|y|Y|yes|YES|Yes)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

select_deployment_mode() {
  local answer=""

  if [ -n "$deployment_mode" ]; then
    return
  fi
  if ! read -r -p "部署模式 [1=HTTP（默认）, 2=HTTPS]: " answer; then
    echo "[ContentServer] 无法读取交互输入；请传入 --http 或 --https。" >&2
    exit 1
  fi

  case "$answer" in
    ""|1|http|HTTP)
      deployment_mode="http"
      ;;
    2|https|HTTPS)
      deployment_mode="https"
      ;;
    *)
      echo "[ContentServer] 无效部署模式: $answer" >&2
      exit 1
      ;;
  esac
}

cleanup_superseded_image() {
  if [ -n "${previous_image_id:-}" ] && [ "$previous_image_id" != "${current_image_id:-}" ] &&
     [ "$("$container_engine" image inspect --format '{{len .RepoTags}}' "$previous_image_id" 2>/dev/null || true)" = 0 ]; then
    if "$container_engine" image rm "$previous_image_id" >/dev/null; then
      echo "[ContentServer] Removed superseded image: $previous_image_id"
    else
      echo "[ContentServer] 警告: 旧镜像仍被容器引用，未能清理: $previous_image_id" >&2
    fi
  fi
}

echo "[ContentServer] Container engine: $container_engine"
echo "[ContentServer] Image: $IMAGE_REFERENCE"

if [ "$import_choice" = yes ] || { [ "$import_choice" = ask ] && confirm "是否校验并导入镜像？"; }; then
  previous_image_id="$("$container_engine" image inspect --format '{{.Id}}' "$IMAGE_REFERENCE" 2>/dev/null || true)"
  sha256sum -c "$ARCHIVE_NAME.sha256"
  "$container_engine" load -i "$ARCHIVE_NAME"
  current_image_id="$("$container_engine" image inspect --format '{{.Id}}' "$IMAGE_REFERENCE")"
  cleanup_superseded_image
else
  echo "[ContentServer] 已跳过镜像导入。"
fi

if [ "$start_choice" = no ]; then
  echo "[ContentServer] 已跳过容器启动。"
  exit 0
fi
if [ "$start_choice" = ask ] && ! confirm "是否启动 ContentServer？"; then
  echo "[ContentServer] 已跳过容器启动。"
  exit 0
fi

if ! "$container_engine" image inspect "$IMAGE_REFERENCE" >/dev/null 2>&1; then
  echo "[ContentServer] 本地不存在镜像: $IMAGE_REFERENCE" >&2
  exit 1
fi

select_deployment_mode

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

if [ "$deployment_mode" = http ]; then
  "${compose_command[@]}" -f compose.yaml --profile https stop caddy >/dev/null 2>&1 || true
  CONTENTSERVER_BIND_ADDRESS="${CONTENTSERVER_BIND_ADDRESS:-0.0.0.0}" \
    "${compose_command[@]}" -f compose.yaml up -d content-server
  cleanup_superseded_image
  echo "[ContentServer] ContentServer 已通过 HTTP 启动。"
  echo "[ContentServer] 警告: HTTP 通信未加密，请勿通过不可信网络使用管理或发布 Key。"
  exit 0
fi

mkdir -p CaddyData CaddyConfig
CONTENTSERVER_BIND_ADDRESS="${CONTENTSERVER_BIND_ADDRESS:-127.0.0.1}" \
  "${compose_command[@]}" -f compose.yaml --profile https up -d
cleanup_superseded_image

hostname="${CONTENTSERVER_HOSTNAME:-content.dev.scnet}"
root_certificate="CaddyData/caddy/pki/authorities/local/root.crt"
for _ in {1..30}; do
  if [ -f "$root_certificate" ]; then
    cp "$root_certificate" caddy-root.crt
    chmod 644 caddy-root.crt
    break
  fi
  sleep 1
done

echo "[ContentServer] ContentServer 已启动: https://$hostname"
echo "[ContentServer] 请在客户端 hosts 中配置: <服务器 IP> $hostname"
if [ -f caddy-root.crt ]; then
  echo "[ContentServer] 内部 CA 根证书: $SCRIPT_DIR/caddy-root.crt"
  echo "[ContentServer] 在客户端信任该证书后再访问服务。"
else
  echo "[ContentServer] 警告: 尚未找到 Caddy 根证书，请检查 Caddy 日志。" >&2
fi
