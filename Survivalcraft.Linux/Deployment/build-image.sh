#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
publish_dir="${SCNET_PUBLISH_DIR:?MSBuild did not provide SCNET_PUBLISH_DIR}"
version="${SCNET_VERSION:?MSBuild did not provide SCNET_VERSION}"
output_dir="${OUTPUT_DIR:-$root/Publish}"
image_name="${IMAGE_NAME:-localhost/scnet/game-server}"
image_tag="${IMAGE_TAG:-$version}"
pull_policy="${IMAGE_PULL_POLICY:-never}"

case "$pull_policy" in never|missing|always) ;; *) echo "Invalid IMAGE_PULL_POLICY: $pull_policy" >&2; exit 2 ;; esac
if [[ ! "$image_tag" =~ ^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$ ]]; then
    echo "Invalid image tag: $image_tag" >&2
    exit 2
fi
if [[ ! "$image_name" =~ ^[A-Za-z0-9_./-]+$ ]]; then
    echo "Invalid image name: $image_name" >&2
    exit 2
fi

if [[ -n "${CONTAINER_ENGINE:-}" ]]; then
    case "$CONTAINER_ENGINE" in podman|docker) engine="$CONTAINER_ENGINE" ;; *) echo "Invalid CONTAINER_ENGINE" >&2; exit 2 ;; esac
elif command -v podman >/dev/null 2>&1; then
    engine=podman
elif command -v docker >/dev/null 2>&1; then
    engine=docker
else
    echo "Podman or Docker is required to publish the game server image." >&2
    exit 1
fi
for executable in "$engine" git sha256sum tar gzip; do
    command -v "$executable" >/dev/null 2>&1 || { echo "Missing build tool: $executable" >&2; exit 1; }
done
container_command=("$engine")
if [[ "$engine" == podman ]] && command -v fuse-overlayfs >/dev/null 2>&1; then
    container_command+=(--storage-opt "overlay.mount_program=$(command -v fuse-overlayfs)")
fi
for file in SurvivalcraftStarter Content.zip; do
    [[ -f "$publish_dir/$file" ]] || { echo "Missing publish output: $file" >&2; exit 1; }
done

reference="$image_name:$image_tag"
revision="$(git -C "$root" rev-parse --short=12 HEAD)"
if ! git -C "$root" diff --quiet || ! git -C "$root" diff --cached --quiet; then
    revision="$revision-dirty"
fi
temporary_dir="$(mktemp -d)"
trap 'rm -rf -- "$temporary_dir"' EXIT
mkdir -p "$temporary_dir/bundle" "$output_dir"

"${container_command[@]}" build --pull="$pull_policy" --platform linux/amd64 \
    --build-arg "SCNET_VERSION=$version" --build-arg "SOURCE_REVISION=$revision" \
    -f "$root/Survivalcraft.Linux/Deployment/Dockerfile" -t "$reference" "$publish_dir"

archive="game-server-image-$image_tag-linux-amd64.tar"
bundle="game-server-$image_tag-linux-amd64.tar.gz"
if [[ "$engine" == podman ]]; then
    "${container_command[@]}" save --format docker-archive "$reference" > "$temporary_dir/bundle/$archive"
else
    "${container_command[@]}" save "$reference" > "$temporary_dir/bundle/$archive"
fi
(
    cd "$temporary_dir/bundle"
    sha256sum "$archive" > "$archive.sha256"
)
sed "s|__SCNET_IMAGE__|$reference|g" \
    "$root/Survivalcraft.Linux/Deployment/compose.yaml" > "$temporary_dir/bundle/compose.yaml"
sed -e "s|__SCNET_IMAGE__|$reference|g" -e "s|__SCNET_ARCHIVE__|$archive|g" \
    "$root/Survivalcraft.Linux/Deployment/deploy.sh" > "$temporary_dir/bundle/deploy.sh"
cp "$root/Survivalcraft.Linux/Deployment/server.conf.example" "$temporary_dir/bundle/server.conf.example"
cp "$root/Survivalcraft.Linux/Deployment/configure.py" "$temporary_dir/bundle/configure.py"
chmod +x "$temporary_dir/bundle/deploy.sh"
tar -C "$temporary_dir/bundle" --sort=name --mtime='UTC 1970-01-01' \
    --owner=0 --group=0 --numeric-owner -cf - . | gzip -n -9 > "$output_dir/$bundle.tmp"
mv "$output_dir/$bundle.tmp" "$output_dir/$bundle"
echo "[SCNET] Game server image: $reference"
echo "[SCNET] Game server bundle: $output_dir/$bundle"
