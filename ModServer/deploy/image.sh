#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"

IMAGE_REPOSITORY="${MOD_SERVER_IMAGE:-registry.cn-hangzhou.aliyuncs.com/yunus4832/mod_server}"
IMAGE_TAG="${MOD_SERVER_TAG:-$(sed -n 's:.*<ScNetworkVersion>\(.*\)</ScNetworkVersion>.*:\1:p' "$ROOT_DIR/Directory.Build.props" | head -n 1)}"
PUSH_LATEST="${PUSH_LATEST:-1}"
BUILD_PULL_POLICY="${BUILD_PULL_POLICY:-never}"

if [[ -z "$IMAGE_TAG" ]]; then
  echo "Unable to determine image tag. Set MOD_SERVER_TAG explicitly." >&2
  exit 1
fi

if [[ -n "${CONTAINER_TOOL:-}" ]]; then
  TOOL="$CONTAINER_TOOL"
elif command -v docker >/dev/null 2>&1; then
  TOOL="docker"
elif command -v podman >/dev/null 2>&1; then
  TOOL="podman"
else
  echo "Neither docker nor podman was found. Set CONTAINER_TOOL to the container CLI." >&2
  exit 1
fi

usage() {
  cat <<EOF
Usage: $(basename "$0") build|push|build-push

Environment:
  MOD_SERVER_IMAGE   Image repository. Default: $IMAGE_REPOSITORY
  MOD_SERVER_TAG     Image tag. Default: ScNetworkVersion from Directory.Build.props
  CONTAINER_TOOL     docker or podman. Default: auto-detect
  BUILD_PULL_POLICY  Base image pull policy for build. Default: never
  PUSH_LATEST        Push latest tag too. Default: 1
EOF
}

build_image() {
  local pull_args=()

  if [[ "$TOOL" == "podman" ]]; then
    pull_args=(--pull="$BUILD_PULL_POLICY")
  elif [[ "$BUILD_PULL_POLICY" == "always" ]]; then
    pull_args=(--pull)
  elif [[ "$BUILD_PULL_POLICY" != "never" && "$BUILD_PULL_POLICY" != "missing" ]]; then
    echo "Unsupported BUILD_PULL_POLICY for docker: $BUILD_PULL_POLICY. Use never, missing, or always." >&2
    exit 1
  fi

  "$TOOL" build \
    "${pull_args[@]}" \
    -f "$SCRIPT_DIR/Dockerfile" \
    -t "$IMAGE_REPOSITORY:$IMAGE_TAG" \
    -t "$IMAGE_REPOSITORY:latest" \
    "$ROOT_DIR"
}

push_image() {
  "$TOOL" push "$IMAGE_REPOSITORY:$IMAGE_TAG"

  if [[ "$PUSH_LATEST" == "1" ]]; then
    "$TOOL" push "$IMAGE_REPOSITORY:latest"
  fi
}

case "${1:-}" in
  build)
    build_image
    ;;
  push)
    push_image
    ;;
  build-push)
    build_image
    push_image
    ;;
  -h|--help|help)
    usage
    ;;
  *)
    usage >&2
    exit 1
    ;;
esac

echo "ModServer image: $IMAGE_REPOSITORY:$IMAGE_TAG"
