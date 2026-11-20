#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
archive_name="__AUTHENTICATIONSERVER_ARCHIVE__"
image_reference="__AUTHENTICATIONSERVER_IMAGE__"
import_image=true
start_container=true
while (($#)); do
  case "$1" in
    --skip-import) import_image=false; shift ;;
    --no-start) start_container=false; shift ;;
    -h|--help) echo "Usage: ./deploy.sh [--skip-import] [--no-start]"; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
done

if [[ -n "${CONTAINER_ENGINE:-}" ]]; then
  container_engine="$CONTAINER_ENGINE"
elif command -v podman >/dev/null 2>&1; then
  container_engine=podman
else
  container_engine=docker
fi
case "$container_engine" in podman|docker) ;; *) echo "CONTAINER_ENGINE must be podman or docker." >&2; exit 2 ;; esac
command -v "$container_engine" >/dev/null
cd "$script_dir"
if $import_image; then
  sha256sum -c "$archive_name.sha256"
  "$container_engine" load -i "$archive_name"
fi
if ! $start_container; then exit 0; fi
if [[ ! -f provider.env ]]; then
  cp provider.env.example provider.env
  chmod 600 provider.env
  echo "Created provider.env. Configure HTTPS public URL, account hashes and trusted proxy before starting." >&2
  exit 2
fi
if [[ ! -f server.conf ]]; then
  cp server.conf.example server.conf
  chmod 600 server.conf
fi
bash -n server.conf
set -a
source server.conf
set +a
"$container_engine" image inspect "$image_reference" >/dev/null
if [[ "$container_engine" == docker ]]; then
  docker compose -f compose.yaml up -d --force-recreate authentication-server
elif podman compose version >/dev/null 2>&1; then
  podman compose -f compose.yaml up -d --force-recreate authentication-server
elif command -v podman-compose >/dev/null 2>&1; then
  podman-compose -f compose.yaml up -d --force-recreate authentication-server
else
  echo "Podman Compose is required." >&2; exit 1
fi
echo "Authentication provider started on loopback. Preserve its named data volume and provider.env."
