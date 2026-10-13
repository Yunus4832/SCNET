#!/usr/bin/env bash
#
# 使用 MSBuild 准备好的 linux publish 目录，通过 Podman 或 Docker 组装镜像 bundle。
#
# 用法：
# 此脚本是 RendezvousServer/Directory.Build.targets 的内部实现，不负责构建或发布项目。
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
IMAGE_NAME="${IMAGE_NAME:-localhost/scnet/rendezvous-server}"
PLATFORM="${PLATFORM:-linux/amd64}"
OUTPUT_DIR="${OUTPUT_DIR:-$ROOT/Publish}"
PUBLISH_DIR="${RENDEZVOUSSERVER_PUBLISH_DIR:?MSBuild 未提供 RENDEZVOUSSERVER_PUBLISH_DIR}"
SCNET_VERSION="${SCNET_VERSION:?MSBuild 未提供 SCNET_VERSION}"
RENDEZVOUS_SERVER_VERSION="$SCNET_VERSION"
DOTNET_SDK_VERSION="${DOTNET_SDK_VERSION:?MSBuild 未提供 DOTNET_SDK_VERSION}"

if [ -n "${CONTAINER_ENGINE:-}" ]; then
  case "$CONTAINER_ENGINE" in
    docker|podman)
      container_engine="$CONTAINER_ENGINE"
      ;;
    *)
      echo "[RendezvousServer] CONTAINER_ENGINE 仅支持 docker 或 podman: $CONTAINER_ENGINE" >&2
      exit 1
      ;;
  esac
elif command -v podman >/dev/null 2>&1; then
  container_engine="podman"
elif command -v docker >/dev/null 2>&1; then
  container_engine="docker"
else
  echo "[RendezvousServer] 缺少容器引擎: 请安装 Podman 或 Docker。" >&2
  exit 1
fi

for command_name in awk git gzip sha256sum tar "$container_engine"; do
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "[RendezvousServer] 缺少构建工具: $command_name" >&2
    exit 1
  fi
done

case "$PLATFORM" in
  linux/amd64)
    artifact_architecture="linux-amd64"
    ;;
  linux/arm64)
    artifact_architecture="linux-arm64"
    ;;
  *)
    echo "[RendezvousServer] 不支持的镜像平台: $PLATFORM" >&2
    exit 1
    ;;
esac

source_revision="$(git -C "$ROOT" rev-parse --short=12 HEAD)"
dirty_suffix=""
if ! git -C "$ROOT" diff --quiet || ! git -C "$ROOT" diff --cached --quiet ||
   [ -n "$(git -C "$ROOT" ls-files --others --exclude-standard)" ]; then
  dirty_suffix="-dirty"
  echo "[RendezvousServer] 警告: 工作区包含未提交变更，产物标记为 dirty。" >&2
fi

image_tag="${IMAGE_TAG:-$RENDEZVOUS_SERVER_VERSION}"
if [[ ! "$image_tag" =~ ^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$ ]]; then
  echo "[RendezvousServer] 镜像标签无效: $image_tag" >&2
  exit 1
fi

# 默认只使用本地基础镜像。让引擎自行解析基础镜像会触发 registry 查询，即使本地已存在同一标签的镜像，
# 也会在离线或受限网络下长时间阻塞发行构建。
image_pull_policy="${IMAGE_PULL_POLICY:-never}"
case "$image_pull_policy" in
  never|missing|always)
    ;;
  *)
    echo "[RendezvousServer] IMAGE_PULL_POLICY 仅支持 never、missing 或 always: $image_pull_policy" >&2
    exit 1
    ;;
esac

build_date="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
image_reference="$IMAGE_NAME:$image_tag"
temporary_root="$(mktemp -d "$ROOT/Publish/.rendezvous-server-image.XXXXXX")"
container_command=("$container_engine")
if [ "$container_engine" = podman ] && command -v fuse-overlayfs >/dev/null 2>&1; then
  container_command+=(--storage-opt overlay.mount_program="$(command -v fuse-overlayfs)")
fi
previous_image_id="$("${container_command[@]}" image inspect --format '{{.Id}}' "$image_reference" 2>/dev/null || true)"

cleanup() {
  rm -rf -- "$temporary_root"
}
trap cleanup EXIT

echo "[RendezvousServer] Source revision: $source_revision$dirty_suffix"
echo "[RendezvousServer] RendezvousServer version: $RENDEZVOUS_SERVER_VERSION"
echo "[RendezvousServer] SCNET version: $SCNET_VERSION"
echo "[RendezvousServer] .NET SDK version: $DOTNET_SDK_VERSION"
echo "[RendezvousServer] Container engine: $container_engine"
echo "[RendezvousServer] Base image pull policy: $image_pull_policy"
echo "[RendezvousServer] Publish directory: $PUBLISH_DIR"

for required_path in RendezvousServer.dll appsettings.json; do
  if [ ! -f "$PUBLISH_DIR/$required_path" ]; then
    echo "[RendezvousServer] 发布产物缺失: $required_path" >&2
    exit 1
  fi
done

if find "$PUBLISH_DIR" -type f \( -name '*.db' -o -name '*.scpkg' \) -print -quit | grep -q .; then
  echo "[RendezvousServer] 发布目录意外包含业务数据。" >&2
  exit 1
fi

# 构建机可能使用严格 umask；统一只读应用文件权限，避免运行环境无法读取静态资源。
chmod -R u=rwX,go=rX "$PUBLISH_DIR"

echo "[RendezvousServer] Building image: $image_reference"
"${container_command[@]}" build \
  --pull="$image_pull_policy" \
  --platform "$PLATFORM" \
  --build-arg "BUILD_DATE=$build_date" \
  --build-arg "RENDEZVOUS_SERVER_VERSION=$RENDEZVOUS_SERVER_VERSION" \
  --build-arg "SCNET_VERSION=$SCNET_VERSION" \
  --build-arg "SOURCE_REVISION=$source_revision$dirty_suffix" \
  --label "org.scnet.build.dotnet=$DOTNET_SDK_VERSION" \
  -f "$ROOT/RendezvousServer/Deployment/Dockerfile" \
  -t "$image_reference" \
  "$PUBLISH_DIR"

current_image_id="$("${container_command[@]}" image inspect --format '{{.Id}}' "$image_reference")"
if [ -n "$previous_image_id" ] && [ "$previous_image_id" != "$current_image_id" ] &&
   [ "$("${container_command[@]}" image inspect --format '{{len .RepoTags}}' "$previous_image_id")" = 0 ]; then
  if "${container_command[@]}" image rm "$previous_image_id" >/dev/null; then
    echo "[RendezvousServer] Removed superseded image: $previous_image_id"
  else
    echo "[RendezvousServer] 警告: 旧镜像仍被容器引用，未能清理: $previous_image_id" >&2
  fi
fi

deployment_directory="$temporary_root/deployment"
mkdir -p "$deployment_directory" "$OUTPUT_DIR"
image_archive_name="rendezvous-server-image-$image_tag-$artifact_architecture.tar"
image_archive_path="$deployment_directory/$image_archive_name"
bundle_name="rendezvous-server-$image_tag-$artifact_architecture.tar.gz"
bundle_path="$OUTPUT_DIR/$bundle_name"
temporary_bundle="$bundle_path.tmp"

echo "[RendezvousServer] Exporting image: $image_archive_name"
if [ "$container_engine" = podman ]; then
  "${container_command[@]}" save --format docker-archive "$image_reference" > "$image_archive_path"
else
  "${container_command[@]}" save "$image_reference" > "$image_archive_path"
fi
(
  cd "$deployment_directory"
  sha256sum "$image_archive_name" > "$image_archive_name.sha256"
)

awk -v image_reference="$image_reference" \
  '{gsub(/__RENDEZVOUSSERVER_IMAGE__/, image_reference); print}' \
  "$ROOT/RendezvousServer/Deployment/compose.yaml" > "$deployment_directory/compose.yaml"
awk -v archive_name="$image_archive_name" -v image_reference="$image_reference" \
  '{gsub(/__RENDEZVOUSSERVER_ARCHIVE__/, archive_name); gsub(/__RENDEZVOUSSERVER_IMAGE__/, image_reference); print}' \
  "$ROOT/RendezvousServer/Deployment/deploy.sh" > "$deployment_directory/deploy.sh"
cp "$ROOT/RendezvousServer/Deployment/server.conf.example" "$deployment_directory/server.conf.example"
chmod 644 "$image_archive_path" "$image_archive_path.sha256" \
  "$deployment_directory/compose.yaml" "$deployment_directory/server.conf.example"
chmod 755 "$deployment_directory/deploy.sh"

echo "[RendezvousServer] Creating bundle: $bundle_path"
tar -C "$deployment_directory" --sort=name --mtime='UTC 1970-01-01' \
  --owner=0 --group=0 --numeric-owner -cf - . | gzip -n -9 > "$temporary_bundle"
mv "$temporary_bundle" "$bundle_path"
chmod 644 "$bundle_path"

echo "[RendezvousServer] Image: $image_reference"
echo "[RendezvousServer] Bundle: $bundle_path"
